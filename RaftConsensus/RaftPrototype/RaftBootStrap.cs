﻿using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TeamDecided.RaftConsensus;
using TeamDecided.RaftConsensus.Interfaces;

namespace RaftPrototype
{
    public partial class RaftBootStrap : Form
    {
        private const int MAXIMUM_NODES = 9;
        private const int MINIMUM_NODES = 3;
        private const int DEFAULT_NODES = 5;
        private const int START_PORT = 5555;

        private const string MAX_NODES_WARNING = "Maximum nine (9) nodes supported in prototype";
        private const string MIN_NODES_WARNING = "Consensus requires minimum three (3) nodes";
        private const string CLUSTER_NAME = "Prototype Cluster";
        private const string CLUSTER_PASSWD = "password";
        private const string IP_TO_BIND = "127.0.0.1";

        private string configFile = "./config.json";

        List<Tuple<string, string, int>> config = new List<Tuple<string, string, int>>();

        protected StatusBar mainStatusBar = new StatusBar();
        protected StatusBarPanel statusPanel = new StatusBarPanel();
        protected StatusBarPanel datetimePanel = new StatusBarPanel();

        //////private IConsensus<string, string>[] nodes;
        //private RaftNode[] servers;

        public RaftBootStrap()
        {
            InitializeComponent();
            Initialize();
        }

        private void Initialize()
        {
            //add defaults to GUI
            tbClusterName.Text = CLUSTER_NAME;
            tbClusterPasswd.Text = CLUSTER_PASSWD;
            tbPort.Text = START_PORT.ToString();
            tbIPAddress.Text = IP_TO_BIND;
            tbIPAddress.Enabled = false;//don't want the user to change this at the moment

            //nodes = RaftConsensus<string, string>.MakeNodesForTest(5, START_PORT);

            // setup node numeric up down UI
            SetNodeCountSelector();
            // setup status bar
            CreateStatusBar();
            //Populate the datagrid with nodeName, ipAddress and port information
            CreateGridView();
        }

        private void PopulateNodeInformation(RaftConsensus<string, string>[] nodes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                for (int j = 0; j < nodes.Length; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    nodes[i].ManualAddPeer(nodes[j].GetNodeName(), new IPEndPoint(IPAddress.Parse(IP_TO_BIND), START_PORT + j));
                }
            }
        }
        
        private void SetNodeCountSelector()
        {
            // Set the Minimum, Maximum, and initial Value.
            nNodes.Value = DEFAULT_NODES;
            nNodes.Maximum = MAXIMUM_NODES;
            nNodes.Minimum = MINIMUM_NODES;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void nNodes_ValueChanged(object sender, EventArgs e)
        {
            if (nNodes.Value == 9)
            {
                lWarningNodesNumber.ForeColor = Color.Red;
                lWarningNodesNumber.Visible = true;
                statusPanel.Text = MAX_NODES_WARNING;
                lWarningNodesNumber.Text= MAX_NODES_WARNING;
            }
            else if (nNodes.Value == 3)
            {
                lWarningNodesNumber.ForeColor = Color.Red;
                lWarningNodesNumber.Visible = true;
                statusPanel.Text = MIN_NODES_WARNING;
                lWarningNodesNumber.Text = MIN_NODES_WARNING;
            }
            else
            {
                lWarningNodesNumber.Visible = false;
                statusPanel.Text = "";
            }
            nodeConfigDataView.DataSource = null;
            CreateGridView();
        }

        private void CreateStatusBar()

        {
            // Set first panel properties and add to StatusBar
            //statusPanel.BorderStyle = StatusBarPanelBorderStyle.Sunken;
            statusPanel.BorderStyle = StatusBarPanelBorderStyle.Raised;
            //statusPanel.Text = "";//"Application started. No action yet"
            //statusPanel.ToolTipText = "Last Activity";
            statusPanel.AutoSize = StatusBarPanelAutoSize.Spring;
            mainStatusBar.Panels.Add(statusPanel);

            // Set second panel properties and add to StatusBar
            datetimePanel.BorderStyle = StatusBarPanelBorderStyle.Raised;
            datetimePanel.ToolTipText = "DateTime: " + System.DateTime.Today.ToString();
            datetimePanel.Text = System.DateTime.Today.ToLongDateString();
            datetimePanel.AutoSize = StatusBarPanelAutoSize.Contents;
            datetimePanel.Alignment = HorizontalAlignment.Right;

            mainStatusBar.Panels.Add(datetimePanel);
            mainStatusBar.ShowPanels = true;

            // Add StatusBar to Form controls
            this.Controls.Add(mainStatusBar);
        }

        private void btnCreate_Click(object sender, EventArgs e)
        {
            ///Create config file, and save it, then start people
            ///Information for config file is represented by information in GUI

            int maxNodes = (int)nNodes.Value;

            //create a config file structure
            RaftBootstrapConfig rbsc = new RaftBootstrapConfig();
            rbsc.clusterName = tbClusterName.Text;
            rbsc.clusterPassword = tbClusterPasswd.Text;//should this really be plain text!
            rbsc.leaderIP = IP_TO_BIND;
            rbsc.maxNodes = maxNodes;//set max nodes, generic for all

            foreach(var node in config)
            {
                rbsc.nodeNames.Add(node.Item1);
                rbsc.nodeIPAddresses.Add(node.Item2);
                rbsc.nodePorts.Add(node.Item3);
            }

            string json = JsonConvert.SerializeObject(rbsc, Formatting.Indented);

            File.Delete(configFile);
            File.WriteAllText(configFile, json);

            //this.Hide();
            //this is the leader window
            Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location, string.Format("{0} {1}", rbsc.nodeNames[0], configFile));

            //Let's start the leader with a 500ms head start (sleep) beofore starting the rest
            Thread.Sleep(500);

            for (int i = 1; i < rbsc.nodeNames.Count; i++)
            {
                if (i == 1)
                {
                    RaftNode node1 = new RaftNode(rbsc.nodeNames[1], configFile);
                    node1.Show();
                }
                else
                {
                    Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location, string.Format("{0} {1}", rbsc.nodeNames[i], configFile));
                }
            }

            Close();

            //servers = new RaftServer[(int)numericUpDownNodes.Value];

            //for (int i = 0; i < numericUpDownNodes.Value; i++)
            //{
            //    servers[i] = new RaftServer();
            //    servers[i].Show();
            //}
            //RaftServer raftServer = new RaftServer();

            //raftServer.Show();
            //this.Hide();
        }

        private void CreateGridView()
        {
            // temporary datasource
            config = new List<Tuple<string, string, int>>();
            int maxNodes = (int) nNodes.Value;

            for (int i = 0; i< maxNodes; i++)
            {
                string nodeName = string.Format("Node {0}", i + 1);
                string nodeIP = IP_TO_BIND;
                int nodePort = i + START_PORT;

                Tuple<string, string, int> temp = new Tuple<string, string, int> (nodeName, nodeIP, nodePort);
                config.Add(temp);
            }

            nodeConfigDataView.DataSource = config;
        }
    }
}