using MergerLogic.DataTypes;
using MergerLogic.Utils;
using MergerLogicUnitTests.testUtils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("http")]
    [TestCategory("HttpUtils")]
    public class HttpRequestUtilsTest
    {
        #region mocks
        private MockRepository _repository;
        private Mock<HttpMessageHandler> _httpMessageHandler;
        private HttpClient _httpClientMock;
        private Mock<ILogger<IHttpRequestUtils>> _loggerMock;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._httpMessageHandler = this._repository.Create<HttpMessageHandler>();
            this._httpClientMock = new HttpClient(this._httpMessageHandler.Object);
            this._loggerMock = this._repository.Create<ILogger<IHttpRequestUtils>>(MockBehavior.Loose);
        }


        //string? PostDataString(string url, HttpContent? content, bool ignoreNotFound = false);

        //string? PutDataString(string url, HttpContent? content, bool ignoreNotFound = false);

        public enum ErrorType { None, NotFound, GenericError}

        #region GetData

        public static IEnumerable<object[]> GetDataBytesParameters()
        {
            return DynamicDataGenerator.GeneratePrams(new object[][]
            {
                new object[] { ErrorType.None, ErrorType.NotFound, ErrorType.GenericError },
                new object[] { true, false }
            });
        }

        [TestMethod]
        [DynamicData(nameof(GetDataBytesParameters),DynamicDataSourceType.Method)]
        public void GetDataBytes(ErrorType errorType,bool ignoreNotFound)
        {
            const string url = "http://testUrl";
            const string testData = "test";
            this.MockSendRequest(HttpMethod.Get, null, url,errorType, testData);

            var reqUtils = new HttpRequestUtils(this._httpClientMock, this._loggerMock.Object);
            byte[]? res;
            if (errorType == ErrorType.None || (errorType == ErrorType.NotFound && ignoreNotFound))
            {
                res = reqUtils.GetData(url, ignoreNotFound);
                if (errorType == ErrorType.NotFound && ignoreNotFound)
                {
                    Assert.IsNull(res);
                }
                else
                {
                    CollectionAssert.AreEqual(res,Encoding.UTF8.GetBytes(testData));
                }
            }
            else
            {
                Assert.ThrowsException<HttpRequestException>(() => reqUtils.GetData(url, ignoreNotFound));
            }

            this._repository.VerifyAll();
        }

        [TestMethod]
        [DynamicData(nameof(GetDataBytesParameters), DynamicDataSourceType.Method)]
        public void GetDataString(ErrorType errorType, bool ignoreNotFound)
        {
            const string url = "http://testUrl";
            const string testData = "test";
            this.MockSendRequest(HttpMethod.Get, null, url, errorType, testData);

            var reqUtils = new HttpRequestUtils(this._httpClientMock, this._loggerMock.Object);
            string? res;
            if (errorType == ErrorType.None || (errorType == ErrorType.NotFound && ignoreNotFound))
            {
                res = reqUtils.GetDataString(url, ignoreNotFound);
                if (errorType == ErrorType.NotFound && ignoreNotFound)
                {
                    Assert.IsNull(res);
                }
                else
                {
                    Assert.AreEqual(res, testData);
                }
            }
            else
            {
                Assert.ThrowsException<HttpRequestException>(() => reqUtils.GetDataString(url, ignoreNotFound));
            }

            this._repository.VerifyAll();
        }

        class GenericTestType
        {
            public string Message { get; set; }
        }

        [TestMethod]
        [DynamicData(nameof(GetDataBytesParameters), DynamicDataSourceType.Method)]
        public void GetDataGeneric(ErrorType errorType, bool ignoreNotFound)
        {
            const string url = "http://testUrl";
            //const string testData = "{\"Message\":\"test\"}";
            GenericTestType testData = new GenericTestType() { Message = "test" };
            this.MockSendRequest(HttpMethod.Get, null, url, errorType, testData);

            var reqUtils = new HttpRequestUtils(this._httpClientMock, this._loggerMock.Object);
            GenericTestType? res;
            if (errorType == ErrorType.None || (errorType == ErrorType.NotFound && ignoreNotFound))
            {
                res = reqUtils.GetData<GenericTestType>(url, ignoreNotFound);
                if (errorType == ErrorType.NotFound && ignoreNotFound)
                {
                    Assert.IsNull(res);
                }
                else
                {
                    Assert.AreEqual(res.Message, testData.Message);
                }
            }
            else
            {
                Assert.ThrowsException<HttpRequestException>(() => reqUtils.GetData<GenericTestType>(url, ignoreNotFound));
            }

            this._repository.VerifyAll();
        }
        #endregion

        #region helpers

        private void MockSendRequest(HttpMethod method, HttpContent? content, string url, ErrorType errorType,string expectedResponse)
        {
            this._httpMessageHandler.Protected().Setup<HttpResponseMessage>("Send", ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == method 
                && req.Content == content
                    && req.RequestUri.ToString() == new Uri(url).ToString() 
                ), ItExpr.IsAny<CancellationToken>())
                .Returns(new HttpResponseMessage(errorType == ErrorType.None? HttpStatusCode.OK : errorType == ErrorType.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(expectedResponse),
                });
        }

        private void MockSendRequest<T>(HttpMethod method, HttpContent? content, string url, ErrorType errorType, T expectedResponse)
        {
            this._httpMessageHandler.Protected().Setup<HttpResponseMessage>("Send", ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == method
                    && req.Content == content
                    && req.RequestUri.ToString() == new Uri(url).ToString()
                ), ItExpr.IsAny<CancellationToken>())
                .Returns(new HttpResponseMessage(errorType == ErrorType.None ? HttpStatusCode.OK : errorType == ErrorType.NotFound ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest)
                {
                    Content =  JsonContent.Create(expectedResponse),
                });
        }

        #endregion
    }
}
