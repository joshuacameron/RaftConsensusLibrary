﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamDecided.RaftCommon.Logging;
using TeamDecided.RaftConsensus;
using TeamDecided.RaftConsensus.Enums;
using TeamDecided.RaftConsensus.Interfaces;

namespace RaftPrototype
{
    public partial class RaftNode : Form
    {
        private IConsensus<string, string> node;
        private SynchronizationContext mainThread;

        private List<Tuple<string, string>> log;
        private static object updateWindowLockObject = new object();

        private string servername;
        private string serverip;
        private int serverport;
        private string configurationFile;
        private string logfile;

        private static Mutex mutex = new Mutex();
        private bool onClosing;

        public RaftNode(string serverName, string configFile, string logFile)
        {
            //set local attributes
            this.servername = serverName;
            this.configurationFile = configFile;
            this.logfile = logFile;
            this.log = new List<Tuple<string, string>>();

            mainThread = SynchronizationContext.Current;
            if (mainThread == null) { mainThread = new SynchronizationContext(); }


            onClosing = false;

            InitializeComponent();
            Initialize();
        }

        #region Setup Node
        private void Initialize()
        {
            //TODO: This is where we need to get the current IConsensus log
            this.Text = string.Format("{0} - {1}", this.Text, servername);//append servername in title bar of window
            this.btStop.Enabled = false;//disable user action
            this.btStart.Enabled = false;//disable user action
            this.FormBorderStyle = FormBorderStyle.FixedDialog;// disable resizing of window

            SetupLogging(logfile);

            //run the configuration setup on background thread stop GUI from blocking
            Task task = new TaskFactory().StartNew(new Action<object>((test) =>
            {
                LoadConfig();
            }), TaskCreationOptions.None);
        }
        
        /// <summary>
        /// Loads configuration file, instantiate node and update the UI
        /// </summary>
        public void LoadConfig()
        {
            try
            {
                string json = File.ReadAllText(configurationFile);
                RaftBootstrapConfig config = JsonConvert.DeserializeObject<RaftBootstrapConfig>(json);
                //Get the node id from the node name string
                int index = int.Parse(servername.Substring(servername.Length - 1)) - 1;
                //populate the peer information
                RaftLogging.Instance.Info("{0} is adding peers", config.nodeNames[index]);

                serverport = config.nodePorts[index];
                serverip = config.nodeIPAddresses[index];
                
                //always making the first entry the cluster manager (Leader)
                if (config.nodeNames[0] == servername)
                {
                    //Instantiate node and set up peer information
                    //subscribe to RaftLogging Log Info event
                    CreateNode(config, 0);

                    //As this is the leader set up the cluster
                    node.CreateCluster(config.clusterName, config.clusterPassword, config.maxNodes);
                }
                else
                {
                    while (true)
                    {
                        //Instantiate node and set up peer information
                        //subscribe to RaftLogging Log Info event
                        CreateNode(config, index);

                        //call the leader to join cluster
                        Task<EJoinClusterResponse> joinTask = node.JoinCluster(config.clusterName, config.clusterPassword, config.maxNodes);
                        joinTask.Wait();

                        //check the result of the attempt to join the cluster
                        EJoinClusterResponse result = joinTask.Result;
                        if (result == EJoinClusterResponse.ACCEPT)
                        {
                            break;
                        }
                        else
                        {
                            if (MessageBox.Show("Failed to join cluster, do you want to retry?", "Error " + servername, MessageBoxButtons.RetryCancel, MessageBoxIcon.Error) == DialogResult.Retry)
                            {
                                node.Dispose();
                                continue;
                            }
                            else
                            {
                                return;
                            }
                        }
                    }
                }

                //update the main UI
                mainThread.Send((object state) =>
                {
                    lock (updateWindowLockObject)
                    {
                        UpdateNodeWindow();
                    }
                }, null);
            }
            catch (Exception e)
            {
                MessageBox.Show(servername + "\n" + e.ToString());
            }
        }

        /// <summary>
        /// Instantiates the node
        /// </summary>
        /// <param name="config">Configuration object containing the details of cluster membership</param>
        /// <param name="index">The index of the node within the configuration object</param>
        private void CreateNode(RaftBootstrapConfig config, int index)
        {
            //Instantiate node
            node = new RaftConsensus<string, string>(config.nodeNames[index], config.nodePorts[index]);
            //Add peer to the node
            AddPeers(config, index);

            //Subscribe to the node UAS start/stop event
            node.StopUAS += HandleUASStop;
            node.StartUAS += HandleUASStart;
            node.OnNewCommitedEntry += HandleNewCommitEntry;
        }

        

        /// <summary>
        /// Setup logging
        /// </summary>
        /// <param name="logFile">Logfile where to post and read infromation from</param>
        private void SetupLogging(string logFile)
        {
            //string path = string.Format(@"{0}", Environment.CurrentDirectory);
            //string debug = Path.Combine(Environment.CurrentDirectory, "debug.log");
            //string debug = Path.Combine("C:\\Users\\Tori\\Downloads\\debug.log");

            RaftLogging.Instance.OverwriteLoggingFile(logFile);
            RaftLogging.Instance.EnableBuffer(50);
            //RaftLogging.Instance.DeleteExistingLogFile();
            RaftLogging.Instance.SetDoInfo(true);
            RaftLogging.Instance.SetDoDebug(true);

        }

        /// <summary>
        /// Updates the UI
        /// </summary>
        private void UpdateNodeWindow()
        {
            lbNodeName.Text = servername;
            if (node != null && node.IsUASRunning())
            {
                this.lbServerState.Text = "UAS running.";
                this.gbAppendEntry.Enabled = true;
                //this.btStop.Enabled = false;
            }
            else
            {
                this.lbServerState.Text = "UAS not running.";
                this.gbAppendEntry.Enabled = false;
                //this.btStop.Enabled = false;
            }

            logDataGrid.DataSource = null;
            logDataGrid.DataSource = log;
        }
        #endregion

        #region event methods

        private void HandleUASStart(object sender, EventArgs e)
        {
            mainThread.Post((object state) =>
            {
                UpdateNodeWindow();
            }, null);
        }

        private void HandleUASStop(object sender, EStopUASReason e)
        {
            mainThread.Post((object state) =>
            {
                UpdateNodeWindow();
            }, null);
        }

        private void HandleInfoLogUpdate(object sender, string e)
        {
            try
            {
                if (mutex.WaitOne())
                {
                    if (!onClosing)
                    { 
                        mainThread.Post((object state) =>
                        {
                            if (CheckLogEntry(e))
                            {
                                try
                                {
                                    tbLog.AppendText(e);
                                }
                                catch
                                {
                                    Console.WriteLine("bad shit happened");
                                }
                            }
                        }, null);
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private void HandleNewCommitEntry(object sender, Tuple<string, string> e)
        {
            string n = servername;
            log.Add(e);
            mainThread.Post((object state) =>
            {
                UpdateNodeWindow();
            }, null);
        }

        private void Stop_Click(object sender, EventArgs e)
        {
            if (node.IsUASRunning())
            {
                node.Dispose();
                lock (updateWindowLockObject)
                {
                    UpdateNodeWindow();
                }
            }
        }

        private void AppendMessage_Click(object sender, EventArgs e)
        {
            try
            {
                node.AppendEntry(tbKey.Text, tbValue.Text);
                // really need some sort of check here to ensure append entry was successfull
                bool ifAppendEntryIsSuccess = true;
                if (ifAppendEntryIsSuccess)
                {
                    tbKey.Clear();
                    tbValue.Clear();
                }
            }
            catch (InvalidOperationException ex)
            {
                RaftLogging.Instance.Debug(string.Format("{0} {1}", servername, ex.ToString()));
            }
        }

        private void cbDebug_CheckedChanged(object sender, EventArgs e)
        {
            if (cbDebug.Checked)
            {
                RaftLogging.Instance.OnNewLineInfo += HandleInfoLogUpdate;
            }
            else
            {
                RaftLogging.Instance.OnNewLineInfo -= HandleInfoLogUpdate;
            }
        }



        #endregion

        #region utilities

        private bool CheckLogEntry(string logEntryLine)
        {
            if ( logEntryLine.IndexOf(servername) == 15)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region consensus stuff

        private void AddPeers(RaftBootstrapConfig config, int id)
        {
            for (int i = 0; i < config.maxNodes; i++)
            {
                //Add the list of nodes into the PeerList
                if (i == id)
                {
                    continue;
                }
                IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(config.nodeIPAddresses[i]), config.nodePorts[i]);
                node.ManualAddPeer(config.nodeNames[i], ipEndpoint);
            }
        }

        private void AddPeers(RaftBootstrapConfig config)
        {
            for (int i = 0; i < config.maxNodes; i++)
            {
                //Add the list of nodes into the PeerList
                if (string.Equals(config.nodeNames[i], servername))
                {
                    continue;
                }
                IPEndPoint ipEndpoint = new IPEndPoint(IPAddress.Parse(config.nodeIPAddresses[i]), config.nodePorts[i]);
                node.ManualAddPeer(config.nodeNames[i], ipEndpoint);
            }
        }
        
        #endregion



        #region closing

        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                mutex.WaitOne();
                onClosing = true;
                RaftLogging.Instance.OnNewLineInfo -= HandleInfoLogUpdate;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
            base.OnClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            node.Dispose();
            base.OnFormClosed(e);
        }

        #endregion

    }
}

