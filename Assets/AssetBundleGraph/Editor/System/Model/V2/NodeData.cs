using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Security.Cryptography;

using UnityEngine.AssetBundles.GraphTool;
using V1=AssetBundleGraph;

namespace UnityEngine.AssetBundles.GraphTool.DataModel.Version2 {

	[Serializable]
	public class NodeData {

		[System.Serializable]
		public class NodeInstance : SerializedInstance<Node> {

			public NodeInstance() : base() {}
			public NodeInstance(NodeInstance instance): base(instance) {}
			public NodeInstance(Node obj) : base(obj) {}
		}

		[SerializeField] private string m_name;
		[SerializeField] private string m_id;
		[SerializeField] private float m_x;
		[SerializeField] private float m_y;
		[SerializeField] private NodeInstance m_nodeInstance;
		[SerializeField] private List<ConnectionPointData> m_inputPoints; 
		[SerializeField] private List<ConnectionPointData> m_outputPoints;

		private bool m_nodeNeedsRevisit;

		/*
		 * Properties
		 */ 

		public bool NeedsRevisit {
			get {
				return m_nodeNeedsRevisit;
			}
			set {
				m_nodeNeedsRevisit = value;
			}
		}

		public string Name {
			get {
				return m_name;
			}
			set {
				m_name = value;
			}
		}
		public string Id {
			get {
				return m_id;
			}
		}
		public NodeInstance Operation {
			get {
				return m_nodeInstance;
			}
		}

		public float X {
			get {
				return m_x;
			}
			set {
				m_x = value;
			}
		}

		public float Y {
			get {
				return m_y;
			}
			set {
				m_y = value;
			}
		}

		public List<ConnectionPointData> InputPoints {
			get {
				return m_inputPoints;
			}
		}

		public List<ConnectionPointData> OutputPoints {
			get {
				return m_outputPoints;
			}
		}


		/*
		 * Constructor used to create new node from GUI
		 */ 
		public NodeData(string name, Node node, float x, float y) {

			m_id = Guid.NewGuid().ToString();
			m_name = name;
			m_x = x;
			m_y = y;
			m_nodeInstance = new NodeInstance(node);
			m_nodeNeedsRevisit = false;

			m_inputPoints  = new List<ConnectionPointData>();
			m_outputPoints = new List<ConnectionPointData>();

			m_nodeInstance.Object.Initialize(this);
		}

		public NodeData(V1.NodeData v1) {
			//TODO:
			m_id = v1.Id;
			m_name = v1.Name;
			m_x = v1.X;
			m_y = v1.Y;
			m_nodeNeedsRevisit = false;

			m_inputPoints  = new List<ConnectionPointData>();
			m_outputPoints = new List<ConnectionPointData>();

			foreach(var input in v1.InputPoints) {
				m_inputPoints.Add(new ConnectionPointData(input));
			}

			foreach(var output in v1.OutputPoints) {
				m_outputPoints.Add(new ConnectionPointData(output));
			}

			Node n = CreateNodeFromV1NodeData(v1, this);
			m_nodeInstance = new NodeInstance(n);
		}

		/**
		 * Duplicate this node with new guid.
		 */ 
		public NodeData Duplicate (bool keepId = false) {

			var newData = new NodeData(m_name, m_nodeInstance.Clone(), m_x, m_y);
			newData.m_nodeNeedsRevisit = false;

			if(keepId) {
				newData.m_id = m_id;
			}

			return newData;
		}

		public ConnectionPointData AddInputPoint(string label) {
			var p = new ConnectionPointData(label, this, true);
			m_inputPoints.Add(p);
			return p;
		}

		public ConnectionPointData AddOutputPoint(string label) {
			var p = new ConnectionPointData(label, this, false);
			m_outputPoints.Add(p);
			return p;
		}

		public ConnectionPointData AddDefaultInputPoint() {
			var p = m_inputPoints.Find(v => v.Label == Settings.DEFAULT_INPUTPOINT_LABEL);
			if(null == p) {
				p = AddInputPoint(Settings.DEFAULT_INPUTPOINT_LABEL);
			}
			return p;
		}

		public ConnectionPointData AddDefaultOutputPoint() {
			var p = m_outputPoints.Find(v => v.Label == Settings.DEFAULT_OUTPUTPOINT_LABEL);
			if(null == p) {
				p = AddOutputPoint(Settings.DEFAULT_OUTPUTPOINT_LABEL);
			}
			return p;
		}

		public ConnectionPointData FindInputPoint(string id) {
			return m_inputPoints.Find(p => p.Id == id);
		}

		public ConnectionPointData FindOutputPoint(string id) {
			return m_outputPoints.Find(p => p.Id == id);
		}

		public ConnectionPointData FindConnectionPoint(string id) {
			var v = FindInputPoint(id);
			if(v != null) {
				return v;
			}
			return FindOutputPoint(id);
		}

		public bool Validate () {
			return m_nodeInstance.Object != null;
		}

		public bool CompareIgnoreGUIChanges (NodeData rhs) {

			if(m_nodeInstance != rhs.m_nodeInstance) {
				LogUtility.Logger.LogFormat(LogType.Log, "{0} and {1} was different: {2}", Name, rhs.Name, "Node Type");
				return false;
			}

			if(m_inputPoints.Count != rhs.m_inputPoints.Count) {
				LogUtility.Logger.LogFormat(LogType.Log, "{0} and {1} was different: {2}", Name, rhs.Name, "Input Count");
				return false;
			}

			if(m_outputPoints.Count != rhs.m_outputPoints.Count) {
				LogUtility.Logger.LogFormat(LogType.Log, "{0} and {1} was different: {2}", Name, rhs.Name, "Output Count");
				return false;
			}

			foreach(var pin in m_inputPoints) {
				if(rhs.m_inputPoints.Find(x => pin.Id == x.Id) == null) {
					LogUtility.Logger.LogFormat(LogType.Log, "{0} and {1} was different: {2}", Name, rhs.Name, "Input point not found");
					return false;
				}
			}

			foreach(var pout in m_outputPoints) {
				if(rhs.m_outputPoints.Find(x => pout.Id == x.Id) == null) {
					LogUtility.Logger.LogFormat(LogType.Log, "{0} and {1} was different: {2}", Name, rhs.Name, "Output point not found");
					return false;
				}
			}


			return true;
		}

		public static Node CreateNodeFromV1NodeData(V1.NodeData v1, NodeData data) {

			NodeDataImporter imp = null;
			Node n = null;

			switch(v1.Kind) {
			case V1.NodeKind.LOADER_GUI:
				{
					var v = new Loader();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.FILTER_GUI:
				{
					var v = new Filter();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.IMPORTSETTING_GUI:
				{
					var v = new ImportSetting();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.MODIFIER_GUI:
				{
					var v = new Modifier();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.GROUPING_GUI:
				{
					var v = new Grouping();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.PREFABBUILDER_GUI:
				{
					var v = new PrefabBuilder();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.BUNDLECONFIG_GUI:
				{
					var v = new BundleConfigurator();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.BUNDLEBUILDER_GUI:
				{
					var v = new BundleBuilder();
					imp = v;
					n   = v;
				}
				break;
			case V1.NodeKind.EXPORTER_GUI:
				{
					var v = new Exporter();
					imp = v;
					n   = v;
				}
				break;
			}

			n.Initialize(data);
			imp.Import(v1, data);

			return n;
		}
	}
}