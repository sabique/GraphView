﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using GraphView;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Newtonsoft.Json;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading;

namespace GraphViewUnitTest
{
    [TestClass]
    public class TinkPopJsonParser
    {
        [TestMethod]
        public void ResetCollection(String collectionName)
        {
            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
                    "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
                    "GroupMatch", collectionName);
            connection.SetupClient();

            connection.DocDB_finish = false;
            connection.BuildUp();
            while (!connection.DocDB_finish)
                System.Threading.Thread.Sleep(10);

            connection.ResetCollection();

            connection.DocDB_finish = false;
            connection.BuildUp();
            while (!connection.DocDB_finish)
                System.Threading.Thread.Sleep(10);
        }
        [TestMethod]
        public void SpecialDataProcessingTest1()
        {
            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
              "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
              "GroupMatch", "MarvelTest");
            GraphViewGremlinParser parser = new GraphViewGremlinParser();
            ResetCollection("MarvelTest");
            // Insert node from collections=
            String value = "Jim O'Meara (Gaelic footballer)".Replace("'", "\\'");
            String tempSQL = "g.addV('id','30153','properties.id','30152','properties.value','" + value + "','label','Person')";
            //String tempSQL = "g.addV('id','30153','properties.id','30152','properties.value','Jim O\\'Meara (Gaelic footballer)','label','Person')";
            parser.Parse(tempSQL.ToString()).Generate(connection).Next();
            Console.WriteLine(tempSQL);
        }
        [TestMethod]
        public void insertJsonMultiTheadByCountDownlatch()
        {
            int i = 0;
            var lines = File.ReadLines(@"D:\dataset\AzureIOT\graphson-dataset.json");
            int index = 0;
            var nodePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var outEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var inEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in lines)
            {
                JObject root = JObject.Parse(line);
                var nodeIdJ = root["id"];
                var nodeLabelJ = root["label"];
                var nodePropertiesJ = root["properties"];
                var nodeOutEJ = root["outE"];
                var nodeInEJ = root["inE"];

                // parse nodeId
                var nodeIdV = nodeIdJ.First.Next.ToString();
                // parse label
                var nodeLabelV = nodeLabelJ.ToString();
                // parse node properties
                foreach (var property in nodePropertiesJ.Children())
                {
                    if (property.HasValues && property.First.HasValues && property.First.First.Next != null)
                    {
                        var tempPChild = property.First.First.Next.Children();
                        foreach (var child1Properties in tempPChild)
                        {
                            // As no API to get the properties name, make it not general
                            var id = nodeIdJ.Last.ToString();
                            if (id != null)
                            {
                                if (id != null)
                                {
                                    var propertyId = child1Properties["id"];
                                    var node = new Dictionary<String, String>();
                                    nodePropertiesHashMap[id.ToString()] = node;
                                    nodePropertiesHashMap[id.ToString()]["id"] = propertyId.Last.ToString();
                                }
                                var value = child1Properties["value"];
                                if (value != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["value"] = value.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                                var label = nodeLabelJ.ToString();
                                if (label != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["label"] = label.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                            }
                        }
                    }
                }
                // parse outE
                var nString = nodeOutEJ.ToString();
                if (nodeOutEJ.HasValues && nodeOutEJ.ToString().Contains("extends"))
                {
                    var tempE = nodeOutEJ.First.Root;
                    foreach (var outEdge in nodeOutEJ.First.First.Last.Children())
                    {
                        var id = outEdge["id"].First.Next;
                        var inV = outEdge["inV"].First.Next;
                        var edgeString = inV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        outEdgePropertiesHashMap[edgeString] = dic;
                        outEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
                // parse inE
                var inString = nodeInEJ.ToString();
                if (nodeInEJ.HasValues && nodeInEJ.ToString().Contains("shown_as"))
                {
                    var tempE = nodeInEJ.First.Root;
                    foreach (var inEdge in nodeInEJ.First.First.Last.Children())
                    {
                        var id = inEdge["id"].First.Next;
                        var outV = inEdge["outV"].First.Next;
                        var edgeString = outV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        inEdgePropertiesHashMap[edgeString] = dic;
                        inEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
            }

            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
                "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
                "GroupMatch", "MarvelTest");
            GraphViewGremlinParser parser = new GraphViewGremlinParser();
            ResetCollection("MarvelTest");
            // Insert node from collections
            int taskNum = nodePropertiesHashMap.Count;
            CountdownEvent cde = new CountdownEvent(taskNum);
            //ManualResetEvent[] doneEvents = new ManualResetEvent[taskNum];
            foreach (var node in nodePropertiesHashMap)
            {
                InsertNodeObjectDocDB insertObj = new InsertNodeObjectDocDB();
                WaitCallback callBack = new WaitCallback(InsertDoc);
                //insertObj.docs = docs;
                //insertObj.cde = cde;
                //insertObj.index = i;
                //insertObj.jdb = jdb;
                //docsNum += docs.Count;
                //insertObj.tempSQL = tempSQL.ToString();
                insertObj.parser = parser;
                insertObj.connection = connection;
                insertObj.cde = cde;
                insertObj.node = node;
                ThreadPool.QueueUserWorkItem(callBack, insertObj);
                
            }
            cde.Wait();
            cde.Dispose();

            // Insert out edge from collections
            foreach (var edge in outEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                //if (nodePropertiesHashMap.ContainsKey(srcId) && nodePropertiesHashMap.ContainsKey(desId))
                //{
                    //// Insert Src Node
                    //StringBuilder tempSQLSrc = new StringBuilder("g.addV(");
                    //tempSQLSrc.Append("\'id\',");
                    //tempSQLSrc.Append("\'" + srcId + "\',");
                    //tempSQLSrc.Append("\'" + "value" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                    //tempSQLSrc.Append("\'" + "label" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                    //tempSQLSrc.Append(")");
                    //parser.Parse(tempSQLSrc.ToString()).Generate(connection).Next();
                    //// Insert Des Node
                    //StringBuilder tempSQLDes = new StringBuilder("g.addV(");
                    //tempSQLDes.Append("\'id\',");
                    //tempSQLDes.Append("\'" + desId + "\',");
                    //tempSQLDes.Append("\'" + "value" + "\',");
                    //tempSQLDes.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                    //tempSQLSrc.Append("\'" + "label" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                    //tempSQLDes.Append(")");
                    //parser.Parse(tempSQLDes.ToString()).Generate(connection).Next();
                    // Inset Edge
                    StringBuilder edgePropertyList = new StringBuilder(",");
                    edgePropertyList.Append("'id',");
                    edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                    String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addOutE('a','extends','b'" + edgePropertyList.ToString() + ")";
                    parser.Parse(tempInsertSQL).Generate(connection).Next();
                    Console.WriteLine(tempInsertSQL);
                //}
            }
            // Insert in edge from collections
            foreach (var edge in inEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                //if (nodePropertiesHashMap.ContainsKey(srcId) && nodePropertiesHashMap.ContainsKey(desId))
                //{
                    //// Insert Src Node
                    //StringBuilder tempSQLSrc = new StringBuilder("g.addV(");
                    //tempSQLSrc.Append("\'id\',");
                    //tempSQLSrc.Append("\'" + srcId + "\',");
                    //tempSQLSrc.Append("\'" + "value" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                    //tempSQLSrc.Append("\'" + "label" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                    //tempSQLSrc.Append(")");
                    //parser.Parse(tempSQLSrc.ToString()).Generate(connection).Next();
                    //// Insert Des Node
                    //StringBuilder tempSQLDes = new StringBuilder("g.addV(");
                    //tempSQLDes.Append("\'id\',");
                    //tempSQLDes.Append("\'" + desId + "\',");
                    //tempSQLDes.Append("\'" + "value" + "\',");
                    //tempSQLDes.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                    //tempSQLSrc.Append("\'" + "label" + "\',");
                    //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                    //tempSQLDes.Append(")");
                    //parser.Parse(tempSQLDes.ToString()).Generate(connection).Next();
                    // Inset Edge
                    StringBuilder edgePropertyList = new StringBuilder(",");
                    edgePropertyList.Append("'id',");
                    edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                    String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addInE('a','shown_as','b'" + edgePropertyList.ToString() + ")";
                    parser.Parse(tempInsertSQL).Generate(connection).Next();
                    Console.WriteLine(tempInsertSQL);
                //}
            }
        }

        static void InsertDoc(Object state)
        {
            var stateObj = (InsertNodeObjectDocDB)state;
            //string connectionString = @"Data Source=(local);
            //Initial Catalog=JsonTesting;
            //Integrated Security=true;";
            //JsonServerConnection jdb = new JsonServerConnection(connectionString);
            //jdb.Open(true);
            ////var jdb = stateObj.jdb;
            //jdb.InsertJson(stateObj.docs, "collection1");
            //Console.WriteLine("Thread " + stateObj.index + " Insert Document new");
            //stateObj.cde.Signal();

            var node = stateObj.node;
            StringBuilder tempSQL = new StringBuilder("g.addV(");
            tempSQL.Append("\'id\',");
            tempSQL.Append("\'" + node.Key + "\',");
            tempSQL.Append("\'" + "properties.id" + "\',");
            tempSQL.Append("\'" + node.Value["id"] + "\',");
            tempSQL.Append("\'" + "properties.value" + "\',");
            tempSQL.Append("\'" + node.Value["value"] + "\',");
            tempSQL.Append("\'" + "label" + "\',");
            tempSQL.Append("\'" + node.Value["label"] + "\'");
            tempSQL.Append(")");
            Console.WriteLine(tempSQL);
            //var tempSQL = stateObj.tempSQL;
            //var connection = stateObj.connection;
            //var parser = stateObj.parser;
            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
      "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
      "GroupMatch", "MarvelTest");
            GraphViewGremlinParser parser = new GraphViewGremlinParser();
            parser.Parse(tempSQL.ToString()).Generate(connection).Next();
            stateObj.cde.Signal();
        }
        [TestMethod]
        public void parseJsonSingleThread()
        {
            int i = 0;
            var lines = File.ReadLines(@"D:\dataset\AzureIOT\graphson-exception2.json");
            int index = 0;
            var nodePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var outEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var inEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in lines)
            {
                JObject root = JObject.Parse(line);
                var nodeIdJ = root["id"];
                var nodeLabelJ = root["label"];
                var nodePropertiesJ = root["properties"];
                var nodeOutEJ = root["outE"];
                var nodeInEJ = root["inE"];

                // parse nodeId
                var nodeIdV = nodeIdJ.First.Next.ToString();
                // parse label
                var nodeLabelV = nodeLabelJ.ToString();
                // parse node properties
                foreach (var property in nodePropertiesJ.Children())
                {
                    if (property.HasValues && property.First.HasValues && property.First.First.Next != null)
                    {
                        var tempPChild = property.First.First.Next.Children();
                        foreach (var child1Properties in tempPChild)
                        {
                            // As no API to get the properties name, make it not general
                            var id = nodeIdJ.Last.ToString();
                            if (id != null)
                            {
                                if (id != null)
                                {
                                    var propertyId = child1Properties["id"];
                                    var node = new Dictionary<String, String>();
                                    nodePropertiesHashMap[id.ToString()] = node;
                                    nodePropertiesHashMap[id.ToString()]["id"] = propertyId.Last.ToString();
                                }
                                var value = child1Properties["value"];
                                if (value != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["value"] = value.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                                var label = nodeLabelJ.ToString();
                                if (label != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["label"] = label.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                            }
                        }
                    }
                }
                // parse outE
                var nString = nodeOutEJ.ToString();
                if (nodeOutEJ.HasValues && nodeOutEJ.ToString().Contains("extends"))
                {
                    var tempE = nodeOutEJ.First.Root;
                    foreach (var outEdge in nodeOutEJ.First.First.Last.Children())
                    {
                        var id = outEdge["id"].First.Next;
                        var inV = outEdge["inV"].First.Next;
                        var edgeString = inV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        outEdgePropertiesHashMap[edgeString] = dic;
                        outEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
                // parse inE
                var inString = nodeInEJ.ToString();
                if (nodeInEJ.HasValues && nodeInEJ.ToString().Contains("shown_as"))
                {
                    var tempE = nodeInEJ.First.Root;
                    foreach (var inEdge in nodeInEJ.First.First.Last.Children())
                    {
                        var id = inEdge["id"].First.Next;
                        var outV = inEdge["outV"].First.Next;
                        var edgeString = outV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        inEdgePropertiesHashMap[edgeString] = dic;
                        inEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
            }

            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
                "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
                "GroupMatch", "MarvelTest");
            GraphViewGremlinParser parser = new GraphViewGremlinParser();
            ResetCollection("MarvelTest");
            // Insert node from collections
            foreach (var node in nodePropertiesHashMap)
            {
                StringBuilder tempSQL = new StringBuilder("g.addV(");
                tempSQL.Append("\'id\',");
                tempSQL.Append("\'" + node.Key + "\',");
                tempSQL.Append("\'" + "properties.id" + "\',");
                tempSQL.Append("\'" + node.Value["id"] + "\',");
                tempSQL.Append("\'" + "properties.value" + "\',");
                tempSQL.Append("\'" + node.Value["value"] + "\',");
                tempSQL.Append("\'" + "label" + "\',");
                tempSQL.Append("\'" + node.Value["label"] + "\'");
                tempSQL.Append(")");
                Console.WriteLine(tempSQL);
                parser.Parse(tempSQL.ToString()).Generate(connection).Next();
            }

            // Insert out edge from collections
            foreach (var edge in outEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                //if (nodePropertiesHashMap.ContainsKey(srcId) && nodePropertiesHashMap.ContainsKey(desId))
                //{
                //// Insert Src Node
                //StringBuilder tempSQLSrc = new StringBuilder("g.addV(");
                //tempSQLSrc.Append("\'id\',");
                //tempSQLSrc.Append("\'" + srcId + "\',");
                //tempSQLSrc.Append("\'" + "value" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                //tempSQLSrc.Append("\'" + "label" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                //tempSQLSrc.Append(")");
                //parser.Parse(tempSQLSrc.ToString()).Generate(connection).Next();
                //// Insert Des Node
                //StringBuilder tempSQLDes = new StringBuilder("g.addV(");
                //tempSQLDes.Append("\'id\',");
                //tempSQLDes.Append("\'" + desId + "\',");
                //tempSQLDes.Append("\'" + "value" + "\',");
                //tempSQLDes.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                //tempSQLSrc.Append("\'" + "label" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                //tempSQLDes.Append(")");
                //parser.Parse(tempSQLDes.ToString()).Generate(connection).Next();
                // Inset Edge
                StringBuilder edgePropertyList = new StringBuilder(",");
                edgePropertyList.Append("'id',");
                edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addOutE('a','extends','b'" + edgePropertyList.ToString() + ")";
                parser.Parse(tempInsertSQL).Generate(connection).Next();
                Console.WriteLine(tempInsertSQL);
                //}
            }
            // Insert in edge from collections
            foreach (var edge in inEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                //if (nodePropertiesHashMap.ContainsKey(srcId) && nodePropertiesHashMap.ContainsKey(desId))
                //{
                //// Insert Src Node
                //StringBuilder tempSQLSrc = new StringBuilder("g.addV(");
                //tempSQLSrc.Append("\'id\',");
                //tempSQLSrc.Append("\'" + srcId + "\',");
                //tempSQLSrc.Append("\'" + "value" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                //tempSQLSrc.Append("\'" + "label" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                //tempSQLSrc.Append(")");
                //parser.Parse(tempSQLSrc.ToString()).Generate(connection).Next();
                //// Insert Des Node
                //StringBuilder tempSQLDes = new StringBuilder("g.addV(");
                //tempSQLDes.Append("\'id\',");
                //tempSQLDes.Append("\'" + desId + "\',");
                //tempSQLDes.Append("\'" + "value" + "\',");
                //tempSQLDes.Append("\'" + nodePropertiesHashMap[srcId]["value"] + "\'");
                //tempSQLSrc.Append("\'" + "label" + "\',");
                //tempSQLSrc.Append("\'" + nodePropertiesHashMap[srcId]["label"] + "\'");
                //tempSQLDes.Append(")");
                //parser.Parse(tempSQLDes.ToString()).Generate(connection).Next();
                // Inset Edge
                StringBuilder edgePropertyList = new StringBuilder(",");
                edgePropertyList.Append("'id',");
                edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addInE('a','shown_as','b'" + edgePropertyList.ToString() + ")";
                parser.Parse(tempInsertSQL).Generate(connection).Next();
                Console.WriteLine(tempInsertSQL);
                //}
            }
        }
        [TestMethod]
        public void InsertJsonMultiThreadByBoundedBuffer()
        {
            // parse data
            int i = 0;
            var lines = File.ReadLines(@"D:\dataset\AzureIOT\graphson-dataset.json");
            int index = 0;
            var nodePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var outEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();
            var inEdgePropertiesHashMap = new Dictionary<string, Dictionary<string, string>>();

            foreach (var line in lines)
            {
                JObject root = JObject.Parse(line);
                var nodeIdJ = root["id"];
                var nodeLabelJ = root["label"];
                var nodePropertiesJ = root["properties"];
                var nodeOutEJ = root["outE"];
                var nodeInEJ = root["inE"];

                // parse nodeId
                var nodeIdV = nodeIdJ.First.Next.ToString();
                // parse label
                var nodeLabelV = nodeLabelJ.ToString();
                // parse node properties
                foreach (var property in nodePropertiesJ.Children())
                {
                    if (property.HasValues && property.First.HasValues && property.First.First.Next != null)
                    {
                        var tempPChild = property.First.First.Next.Children();
                        foreach (var child1Properties in tempPChild)
                        {
                            // As no API to get the properties name, make it not general
                            var id = nodeIdJ.Last.ToString();
                            if (id != null)
                            {
                                if (id != null)
                                {
                                    var propertyId = child1Properties["id"];
                                    var node = new Dictionary<String, String>();
                                    nodePropertiesHashMap[id.ToString()] = node;
                                    nodePropertiesHashMap[id.ToString()]["id"] = propertyId.Last.ToString();
                                }
                                var value = child1Properties["value"];
                                if (value != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["value"] = value.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                                var label = nodeLabelJ.ToString();
                                if (label != null)
                                {
                                    nodePropertiesHashMap[id.ToString()]["label"] = label.ToString().Replace("'", "\\'").Replace("\"", "\\\"");
                                }
                            }
                        }
                    }
                }
                // parse outE
                var nString = nodeOutEJ.ToString();
                if (nodeOutEJ.HasValues && nodeOutEJ.ToString().Contains("extends"))
                {
                    var tempE = nodeOutEJ.First.Root;
                    foreach (var outEdge in nodeOutEJ.First.First.Last.Children())
                    {
                        var id = outEdge["id"].First.Next;
                        var inV = outEdge["inV"].First.Next;
                        var edgeString = inV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        outEdgePropertiesHashMap[edgeString] = dic;
                        outEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
                // parse inE
                var inString = nodeInEJ.ToString();
                if (nodeInEJ.HasValues && nodeInEJ.ToString().Contains("shown_as"))
                {
                    var tempE = nodeInEJ.First.Root;
                    foreach (var inEdge in nodeInEJ.First.First.Last.Children())
                    {
                        var id = inEdge["id"].First.Next;
                        var outV = inEdge["outV"].First.Next;
                        var edgeString = outV + "_" + nodeIdJ.Last();
                        var dic = new Dictionary<string, string>();
                        inEdgePropertiesHashMap[edgeString] = dic;
                        inEdgePropertiesHashMap[edgeString].Add("id", id.ToString());
                    }
                }
            }

            GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
                "MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
                "GroupMatch", "MarvelTest");
            GraphViewGremlinParser parser = new GraphViewGremlinParser();
            ResetCollection("MarvelTest");
            // Insert node from collections
            BoundedBuffer<string> inputBuffer = new BoundedBuffer<string>(10000);

            long startTime = DateTime.Now.Millisecond;
            int threadNum = 100;
            string _inputDataPath = @"D:\dataset\AzureIOT\graphson-exception2.json";
            List<Thread> insertThreadList = new List<Thread>();

            for (int j = 0; j < threadNum; j++)
            {
                DocDBInsertWorker worker1 = new DocDBInsertWorker(inputBuffer);
                worker1.threadId = j;
                Thread t1 = new Thread(worker1.BulkInsert);
                insertThreadList.Add(t1);
            }

            for (int j = 0; j < threadNum; j++)
            {
                insertThreadList[j].Start();
                Console.WriteLine("Start the thread" + j);
            }

            // add node to input buffer
            foreach (var node in nodePropertiesHashMap)
            {
                StringBuilder tempSQL = new StringBuilder("g.addV(");
                tempSQL.Append("\'id\',");
                tempSQL.Append("\'" + node.Key + "\',");
                tempSQL.Append("\'" + "properties.id" + "\',");
                tempSQL.Append("\'" + node.Value["id"] + "\',");
                tempSQL.Append("\'" + "properties.value" + "\',");
                tempSQL.Append("\'" + node.Value["value"] + "\',");
                tempSQL.Append("\'" + "label" + "\',");
                tempSQL.Append("\'" + node.Value["label"] + "\'");
                tempSQL.Append(")");
                Console.WriteLine(tempSQL);
                inputBuffer.Add(tempSQL.ToString());
            }

            // Insert out edge from collections
            foreach (var edge in outEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                StringBuilder edgePropertyList = new StringBuilder(",");
                edgePropertyList.Append("'id',");
                edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addOutE('a','extends','b'" + edgePropertyList.ToString() + ")";
                inputBuffer.Add(tempInsertSQL);
                Console.WriteLine(tempInsertSQL);
            }
            
            // Insert in edge from collections
            foreach (var edge in inEdgePropertiesHashMap)
            {
                String[] nodeIds = edge.Key.Split('_');
                String srcId = nodeIds[0];
                String desId = nodeIds[1];
                StringBuilder edgePropertyList = new StringBuilder(",");
                edgePropertyList.Append("'id',");
                edgePropertyList.Append("'" + edge.Value["id"].ToString() + "'");
                String tempInsertSQL = "g.V.as('v').has('id','" + srcId + "').as('a').select('v').has('id','" + desId + "').as('b').select('a','b').addInE('a','shown_as','b'" + edgePropertyList.ToString() + ")";
                inputBuffer.Add(tempInsertSQL);
                Console.WriteLine(tempInsertSQL);
            }

            inputBuffer.Close();
            Console.WriteLine("Finish init the dataset");

            for (int j = 0; j < threadNum; j++)
            {
                insertThreadList[j].Join();
            }
            
            for (int j = 0; j < threadNum; j++)
            {
                insertThreadList[j].Abort();
            }
        }

    }
    public class InsertNodeObjectDocDB
    {
        public string tempSQL;
        public GraphViewGremlinParser parser;
        public GraphViewConnection connection;
        public CountdownEvent cde = null;
        public KeyValuePair<string, Dictionary<string, string>> node;
    }

    public class DocDBInsertWorker
    {
        BoundedBuffer<string> inputStream;
        //public int bulkInsertSize = 1000;
        //public MongoClient client = null;
        //public IMongoDatabase mongoDB = null;
        //public IMongoCollection<BsonDocument> collec = null;
        public int threadId;
        GraphViewGremlinParser parser = new GraphViewGremlinParser();
        GraphViewConnection connection = new GraphViewConnection("https://graphview.documents.azure.com:443/",
"MqQnw4xFu7zEiPSD+4lLKRBQEaQHZcKsjlHxXn2b96pE/XlJ8oePGhjnOofj1eLpUdsfYgEhzhejk2rjH/+EKA==",
"GroupMatch", "MarvelTest");

        public DocDBInsertWorker(
            BoundedBuffer<string> inputStream)
        {
            //client = new MongoClient(connectionString);
            //mongoDB = client.GetDatabase("MongoTestMethod1");
            //collec = mongoDB.GetCollection<BsonDocument>(collection);
            this.inputStream = inputStream;
        }

        public void BulkInsert()
        {
            string doc = inputStream.Retrieve();
            //List<string> docList = new List<string>();
            List<string> docList = new List<string>();
            int docNum = 1;

            while (doc != null)
            {
                //while (docList.Count < bulkInsertSize)
                //{
                //    //docList.Add(BsonDocument.Parse(doc));
                //    docList.Add(doc);
                //    doc = inputStream.Retrieve();
                //    if (doc == null)
                //    {
                //        break;
                //    }
                //}

                //while (docList.Count != 0)
                //{
                //    try
                //    {
                //        //collec.InsertMany(docList);
                //        //Interlocked.Add(ref MongoInsertMultiThreadWorkerVersionTest.insertCount, docList.Count);
                //        //Console.WriteLine("Mongo Thread Insert " + docList.Count + " document");
                //        Console.WriteLine("Mongo Thread {0} insert {1} docs.", threadId, docList.Count);
                //    }
                //    catch (Exception e)
                //    {
                //        throw e;
                //    }

                //    //docList.Clear();
                //}

                doc = inputStream.Retrieve();
                parser.Parse(doc.ToString()).Generate(connection).Next();
                Console.WriteLine("Thread" + threadId + " docCount" + docNum);
                docNum += 1;
            }

            Console.WriteLine("Thread Insert Finish");
        }

        public void Dispose()
        {
        }
    }

    public class BoundedBuffer<T>
    {
        public int bufferSize;
        public Queue<T> boundedBuffer;
        // Whether the queue expects more elements to come
        bool more;

        public bool More
        {
            get { return more; }
        }

        Object _monitor;
        public BoundedBuffer(int bufferSize)
        {
            boundedBuffer = new Queue<T>(bufferSize);
            this.bufferSize = bufferSize;
            more = true;
            _monitor = new object();
        }

        public void Add(T element)
        {
            lock (_monitor)
            {
                while (boundedBuffer.Count == bufferSize)
                {
                    Monitor.Wait(_monitor);
                }

                boundedBuffer.Enqueue(element);
                Monitor.Pulse(_monitor);
            }
        }

        public T Retrieve()
        {
            T element = default(T);

            lock (_monitor)
            {
                while (boundedBuffer.Count == 0 && more)
                {
                    Monitor.Wait(_monitor);
                }

                if (boundedBuffer.Count > 0)
                {
                    element = boundedBuffer.Dequeue();
                    Monitor.Pulse(_monitor);
                }
            }

            return element;
        }

        public void Close()
        {
            lock (_monitor)
            {
                more = false;
                Monitor.PulseAll(_monitor);
            }
        }
    }
}