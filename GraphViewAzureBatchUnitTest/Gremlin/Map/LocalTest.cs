﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace GraphViewAzureBatchUnitTest.Gremlin.Map
{
    [TestClass]
    public class LocalTest : AbstractAzureBatchGremlinTest
    {
        [TestMethod]
        public void VerticesLocalOutECount()
        {
            string query = "g.V().local(__.outE().count())";
            // todo
            //List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            //Console.WriteLine("-------------Test Result-------------");
            //foreach (string result in results)
            //{
            //    Console.WriteLine(result);
            //}
            //var convertResult = results.Select(r => int.Parse(r));
            //var expectedResult = new List<int> { 3, 0, 0, 0, 1, 2 };
            //CheckUnOrderedResults(expectedResult, convertResult);
        }

        [TestMethod]
        public void VertexWithIdLocalBothEKnowsCreatedLimit1()
        {
            string query = "g.V().has('name', 'josh').local(__.bothE('knows', 'created').limit(1)).values('weight')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(double.Parse(results[0]).Equals(1.0D) || double.Parse(results[0]).Equals(0.4D));
        }

        [TestMethod]
        public void VertexWithIdLocalBothELimit1OtherVName()
        {
            string query = "g.V().has('name', 'josh').local(__.bothE().limit(1)).otherV().values('name')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }

            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Equals("marko") || results[0].Equals("ripple") || results[0].Equals("lop"));
        }

        [TestMethod]
        public void VertexWithIdLocalBothELimit2OtherVName()
        {
            string query = "g.V().has('name', 'josh').local(__.bothE().limit(2)).otherV().values('name')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }

            Assert.AreEqual(2, results.Count);
            foreach (var res in results)
            {
                Assert.IsTrue(res.Equals("marko") || res.Equals("ripple") || res.Equals("lop"));
            }
        }

        [TestMethod]
        public void VertexWithIdLocalInEKnowsLimit2OutVName()
        {
            string query = "g.V().local(__.inE('knows').limit(2).outV()).values('name')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }
            Assert.AreEqual(2, results.Count);
            foreach (var res in results)
            {
                Assert.AreEqual("marko", res);
            }
        }

        [TestMethod]
        public void VertexWithIdLocalBothECreatedLimit1()
        {
            string query = "g.V().has('name', 'josh').local(__.bothE('created').limit(1)).values('weight')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(double.Parse(results[0]).Equals(1.0D) || double.Parse(results[0]).Equals(0.4D));
        }

        [TestMethod]
        public void VertexWithIdLocalOutEKnowsLimit1InVName()
        {
            string query = "g.V().has('name', 'marko').local(__.outE('knows').limit(1)).inV().values('name')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }
            Assert.AreEqual(1, results.Count);
            Assert.IsTrue(results[0].Equals("vadas") || results[0].Equals("josh"));
        }

        [TestMethod]
        public void VerticesLocalBothECreatedLimit1OtherVName()
        {
            string query = "g.V().local(__.bothE('created').limit(1)).otherV().values('name')";
            List<string> results = StartAzureBatch.AzureBatchJobManager.TestQuery(query);
            Console.WriteLine("-------------Test Result-------------");
            foreach (string result in results)
            {
                Console.WriteLine(result);
            }
            Assert.AreEqual(5, results.Count);
            foreach (var res in results)
            {
                Assert.IsTrue(res.Equals("marko") || res.Equals("lop") || res.Equals("josh") || res.Equals("ripple") || res.Equals("peter"));
            }
        }
    }
}