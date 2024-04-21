using MergerLogic.Clients;
using MergerLogic.ImageProcessing;
using MergerLogic.Utils;
using MergerService.Models.Tasks;
using MergerService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace MergerLogicUnitTests.Utils
{
  [TestClass]
  [TestCategory("unit")]
  [TestCategory("utils")]
  [DeploymentItem(@"../../../Utils/TestData")]
  public class TaskUtilsTest
  {
    #region mocks

    private MockRepository _mockRepository;

    private Mock<IHttpRequestUtils> _httpClientMock;
    private Mock<IConfigurationManager> _configurationManagerMock;
    private Mock<ILogger<TaskUtils>> _taskUtilsLoggerMock;

    private ActivitySource _testActivitySource;
    private int _testMaxAttempts = 5;
    private string _testJobManagerUrl = "http://testJobManagerUrl";

    #endregion

    [TestInitialize]
    public void BeforeEach()
    {
      this._mockRepository = new MockRepository(MockBehavior.Loose);

      this._httpClientMock = this._mockRepository.Create<IHttpRequestUtils>();
      this._configurationManagerMock = this._mockRepository.Create<IConfigurationManager>();
      this._taskUtilsLoggerMock = this._mockRepository.Create<ILogger<TaskUtils>>();

      this._testActivitySource = new ActivitySource("test");

      this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("TASK", "maxAttempts")).Returns(this._testMaxAttempts);
      this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration("TASK", "jobManagerUrl")).Returns(this._testJobManagerUrl);
    }

    [TestMethod]
    public void WhenGettingTaskByType_ShouldUseCorrectHttpUri()
    {
      var testJobType = "testJobType";
      var testTaskType = "testTaskType";
      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);

      testTaskUtils.GetTask(testJobType, testTaskType);

      var expectedUrl = $"{this._testJobManagerUrl.ToLower()}/tasks/{testJobType}/{testTaskType}/startPending";
      this._httpClientMock.Verify(httpClient => httpClient.PostData(expectedUrl, null, false), Times.Once);
    }

    public static IEnumerable<object[]> GetBadJsonTestParameters()
    {
        yield return new object[] { "bad json" };
        yield return new object[] { File.ReadAllText("invalidTask.json") };
    }

    [TestMethod]
    [DynamicData(nameof(GetBadJsonTestParameters), DynamicDataSourceType.Method)]
    public void WhenGettingMalformedJsonTask_ShouldReturnNull(string json)
    {
      this._httpClientMock.Setup(httpClient => httpClient.PostData(It.IsAny<string>(), It.IsAny<HttpContent?>(), It.IsAny<bool>())).Returns(json);

      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);

      var resultTask = testTaskUtils.GetTask("testJobType", "testTaskType");

      Assert.IsNull(resultTask);
    }

    [TestMethod]
    public void WhenGettingJsonTask_ShouldReturnTaskObject()
    {
      int maxAttempts = this._configurationManagerMock.Object.GetConfiguration<int>("TASK", "maxAttempts");

      string json = File.ReadAllText("validTask.json");
      this._httpClientMock.Setup(httpClient => httpClient.PostData(It.IsAny<string>(), It.IsAny<HttpContent?>(), It.IsAny<bool>())).Returns(json);

      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);

      var resultTask = testTaskUtils.GetTask("testJobType", "testTaskType");

      Assert.IsNotNull(resultTask);
      
      // Validate GUIDs
      Assert.IsTrue(Guid.TryParse(resultTask.Id, out _));
      Assert.IsTrue(Guid.TryParse(resultTask.JobId, out _));

      // Validate dates
      Assert.IsTrue(DateTime.TryParse(resultTask.Created.ToString(), out _));
      Assert.IsTrue(DateTime.TryParse(resultTask.Updated.ToString(), out _));

      Assert.IsNotNull(resultTask.Parameters);
      Assert.IsTrue(resultTask.Parameters.Sources?.Length > 0);
      Assert.IsTrue(resultTask.Parameters.Batches?.Length > 0);
      Assert.IsInstanceOfType(resultTask.Parameters.TargetFormat, typeof(TileFormat));

      Assert.IsTrue(resultTask.Percentage >= 0 && resultTask.Percentage <= 100);
      Assert.IsTrue(resultTask.Attempts >= 0 && resultTask.Attempts <= maxAttempts);
    }

    [TestMethod]
    public void WhenUpdatingTaskCompleted_ShouldSendCorrectStatusAndPercentage()
    {
      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);


      this._httpClientMock.Setup(httpClient => httpClient.PutData(It.IsAny<string>(), It.IsAny<HttpContent?>(), It.IsAny<bool>())).Callback((string _, HttpContent? content, bool _) =>
       {
         var updateParams = JsonConvert.DeserializeObject<UpdateParams>(content!.ReadAsStringAsync().Result)!;
         Assert.AreEqual(Status.COMPLETED, updateParams.Status);
         Assert.AreEqual(100, updateParams.Percentage);
       });


      testTaskUtils.UpdateCompletion("testJobId", "testTaskId", null);
    }

    [TestMethod]
    public void WhenUpdatingTaskRejected_IfResettable_ShouldUpdateAttempt()
    {
      int testAttempt = 0;
      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);


      this._httpClientMock.Setup(httpClient => httpClient.PutData(It.IsAny<string>(), It.IsAny<HttpContent?>(), It.IsAny<bool>())).Callback((string _, HttpContent? content, bool _) =>
       {
         var updateParams = JsonConvert.DeserializeObject<UpdateParams>(content!.ReadAsStringAsync().Result)!;
         Assert.AreEqual(Status.PENDING, updateParams.Status);
         Assert.AreEqual(testAttempt + 1, updateParams.Attempts);
       });


      testTaskUtils.UpdateReject("testJobId", "testTaskId", testAttempt, "testReason", true, null);
    }

    [TestMethod]
    public void WhenUpdatingTaskRejected_IfResettableButTooManyAttempts_ShouldUpdateFailed()
    {
      int testAttempt = this._testMaxAttempts + 1;
      var testTaskUtils = new TaskUtils(_configurationManagerMock.Object, _httpClientMock.Object, _taskUtilsLoggerMock.Object,
        _testActivitySource);


      this._httpClientMock.Setup(httpClient => httpClient.PutData(It.IsAny<string>(), It.IsAny<HttpContent?>(), It.IsAny<bool>())).Callback((string _, HttpContent? content, bool _) =>
       {
         var updateParams = JsonConvert.DeserializeObject<UpdateParams>(content!.ReadAsStringAsync().Result)!;
         Assert.AreEqual(Status.FAILED, updateParams.Status);
       });


      testTaskUtils.UpdateReject("testJobId", "testTaskId", testAttempt, "testReason", true, null);
    }
  }
}
