﻿namespace Unosquare.Labs.EmbedIO.Tests
{
    using Newtonsoft.Json;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using Unosquare.Labs.EmbedIO.Modules;
    using Unosquare.Labs.EmbedIO.Tests.TestObjects;
    using System.Net.Http;

    [TestFixture]
    public class WebApiModuleTest
    {
        protected WebServer WebServer;
        protected string WebServerUrl = Resources.GetServerAddress();
        protected TestConsoleLog Logger = new TestConsoleLog();

        [SetUp]
        public void Init()
        {
            WebServer = new WebServer(WebServerUrl, Logger)
                .WithWebApiController<TestController>();
            WebServer.RunAsync();
        }

        [Test]
        public void TestWebApi()
        {
            Assert.IsNotNull(WebServer.Module<WebApiModule>(), "WebServer has WebApiModule");

            Assert.AreEqual(WebServer.Module<WebApiModule>().ControllersCount, 1, "WebApiModule has one controller");
        }

        [Test]
        public async Task GetJsonData()
        {
            List<Person> remoteList;

            var request = (HttpWebRequest)WebRequest.Create(WebServerUrl + TestController.GetPath);

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");

                var jsonBody = new StreamReader(response.GetResponseStream()).ReadToEnd();

                Assert.IsNotNull(jsonBody, "Json Body is not null");
                Assert.IsNotEmpty(jsonBody, "Json Body is empty");

                remoteList = JsonConvert.DeserializeObject<List<Person>>(jsonBody);

                Assert.IsNotNull(remoteList, "Json Object is not null");
                Assert.AreEqual(remoteList.Count, PeopleRepository.Database.Count, "Remote list count equals local list");
            }

            await TestHelper.ValidatePerson(WebServerUrl + TestController.GetPath + remoteList.First().Key);
        }

        [Test]
        public async Task GetJsonDataWithMiddleUrl()
        {
            var person = PeopleRepository.Database.First();
            await TestHelper.ValidatePerson(WebServerUrl + TestController.GetMiddlePath.Replace("*", person.Key.ToString()));
        }

        [Test]
        public async Task GetJsonAsyncData()
        {
            var person = PeopleRepository.Database.First();
            await TestHelper.ValidatePerson(WebServerUrl + TestController.GetAsyncPath + person.Key);
        }

        [Test]
        public async Task PostJsonData()
        {
            var model = new Person() { Key = 10, Name = "Test" };
            var request = (HttpWebRequest)WebRequest.Create(WebServerUrl + TestController.GetPath);
            request.Method = "POST";

            using (var dataStream = await request.GetRequestStreamAsync())
            {
                var byteArray = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(model));
                dataStream.Write(byteArray, 0, byteArray.Length);
            }

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");

                var jsonString = new StreamReader(response.GetResponseStream()).ReadToEnd();
                Assert.IsNotNull(jsonString);
                Assert.IsNotEmpty(jsonString);

                var json = JsonConvert.DeserializeObject<Person>(jsonString);
                Assert.IsNotNull(json);
                Assert.AreEqual(json.Name, model.Name);
            }
        }

        [Test]
        public async Task TestWebApiWithConstructor()
        {
            const string name = "Test";

            WebServer.Module<WebApiModule>().RegisterController(() => new TestControllerWithConstructor(name));

            var request = (HttpWebRequest)WebRequest.Create(WebServerUrl + "name");

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            {
                Assert.AreEqual(response.StatusCode, HttpStatusCode.OK, "Status Code OK");

                var body = new StreamReader(response.GetResponseStream()).ReadToEnd();

                Assert.AreEqual(body, name);
            }
        }

        [Test]
        public async Task TestDictionaryFormData()
        {
            using (var webClient = new HttpClient())
            {
                var content = new[]
                {
                    new KeyValuePair<string, string>("test", "data"),
                    new KeyValuePair<string, string>("id", "1")
                };

                var formContent = new FormUrlEncodedContent(content);

                var result = await webClient.PostAsync(WebServerUrl + TestController.EchoPath, formContent);
                Assert.IsNotNull(result);
                var data = await result.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(data);
                Assert.AreEqual(2, obj.Keys.Count);

                Assert.AreEqual(content.First().Key, obj.First().Key);
                Assert.AreEqual(content.First().Value, obj.First().Value);
            }
        }

        internal class FormDataSample
        {
            public string test { get; set; }
            public List<string> id { get; set; }
        }

        [TestCase("id", "id")]
        [TestCase("id[0]", "id[1]")]
        public async Task TestMultipleIndexedValuesFormData(string label1, string label2)
        {
            using (var webClient = new HttpClient())
            {
                var content = new[] {
                    new KeyValuePair<string, string>("test", "data"),
                    new KeyValuePair<string, string>(label1, "1"),
                    new KeyValuePair<string, string>(label2, "2")
                };

                var formContent = new FormUrlEncodedContent(content);

                var result = await webClient.PostAsync(WebServerUrl + TestController.EchoPath, formContent);
                Assert.IsNotNull(result);
                var data = await result.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<FormDataSample>(data);
                Assert.IsNotNull(obj);
                Assert.AreEqual(content.First().Value, obj.test);
                Assert.AreEqual(2, obj.id.Count);
                Assert.AreEqual(content.Last().Value, obj.id.Last());
            }
        }

        [TearDown]
        public void Kill()
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
            WebServer.Dispose();
        }
    }
}