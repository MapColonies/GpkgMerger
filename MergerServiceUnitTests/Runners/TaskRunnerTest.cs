using MergerLogic.Batching;
using MergerLogic.Clients;
using MergerLogic.ImageProcessing;
using MergerLogic.Monitoring.Metrics;
using MergerService.Controllers;
using MergerService.Models.Tasks;
using MergerService.Runners;
using MergerService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace MergerLogicUnitTests.Utils
{
  [TestClass]
  [TestCategory("unit")]
  [TestCategory("runners")]
  public class TaskRunnerTest
  {
    #region mocks

    private MockRepository _mockRepository;

    private Mock<IJobUtils> _jobUtilsMock;
    private Mock<ILogger<TaskRunner>> _loggerMock;
    private Mock<ITaskExecutor> _taskExecutorMock;
    private Mock<ITaskUtils> _taskUtilsMock;
    private Mock<IHeartbeatClient> _heartbeatClientMock;
    private Mock<IMetricsProvider> _metricsProviderMock;
    private Mock<MergerLogic.Utils.IConfigurationManager> _configurationManagerMock;

    #endregion

    [TestInitialize]
    public void BeforeEach()
    {
      this._mockRepository = new MockRepository(MockBehavior.Loose);

      this._jobUtilsMock = this._mockRepository.Create<IJobUtils>();
      this._loggerMock = this._mockRepository.Create<ILogger<TaskRunner>>();
      this._taskExecutorMock = this._mockRepository.Create<ITaskExecutor>();
      this._taskUtilsMock = this._mockRepository.Create<ITaskUtils>();
      this._heartbeatClientMock = this._mockRepository.Create<IHeartbeatClient>();
      this._metricsProviderMock = this._mockRepository.Create<IMetricsProvider>();
      this._configurationManagerMock = this._mockRepository.Create<MergerLogic.Utils.IConfigurationManager>();

      this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration<int>("TASK", "maxAttempts")).Returns(1);
    }

    [TestMethod]
    public void WhenTaskRunnerGetsNullTask_ShouldReturnFalseWithoutFailing()
    {
      this._taskUtilsMock.Setup(taskUtils => taskUtils.GetTask(It.IsAny<string>(), It.IsAny<string>())).Returns((MergeTask?)null);

      var testTaskRunner = new TaskRunner(_taskExecutorMock.Object, _jobUtilsMock.Object, _loggerMock.Object,
        _taskUtilsMock.Object, _heartbeatClientMock.Object, _metricsProviderMock.Object,
        _configurationManagerMock.Object);

      var testFetchResult = testTaskRunner.FetchTask(new KeyValuePair<string, string>("testJobType", "testTaskType"));
      var testRunResult = testTaskRunner.RunTask(testFetchResult);

      Assert.IsNull(testFetchResult);
      Assert.IsFalse(testRunResult);
    }

    [TestMethod]
    public void WhenTaskExecutedSuccessfully_ShouldUpdateTaskCompletion()
    {
      var testTask = new MergeTask("testTaskId", "type", "description",
        new MergeMetadata(TileFormat.Jpeg, true, new TileBounds[0], new Source[0]),
        Status.PENDING, 0, "reason", 0, "testJobId", true, new DateTime(), new DateTime());

      this._taskUtilsMock.Setup(taskUtils => taskUtils.GetTask(It.IsAny<string>(), It.IsAny<string>())).Returns(testTask);
      this._taskExecutorMock.Setup(taskExecutor => taskExecutor.ExecuteTask(testTask, _taskUtilsMock.Object, It.IsAny<string?>()));

      var testTaskRunner = new TaskRunner(_taskExecutorMock.Object, _jobUtilsMock.Object, _loggerMock.Object,
        _taskUtilsMock.Object, _heartbeatClientMock.Object, _metricsProviderMock.Object,
        _configurationManagerMock.Object);

      var testResultTask = testTaskRunner.FetchTask(new KeyValuePair<string, string>("testJobType", "testTaskType"));
      testTaskRunner.RunTask(testResultTask);

      Assert.AreEqual(testTask, testResultTask);
      _taskUtilsMock.Verify(taskUtils => taskUtils.UpdateCompletion(testTask.JobId, testTask.Id, It.IsAny<string?>()), Times.Once);
    }

    [TestMethod]
    public void WhenTaskExecutionFailed_ShouldUpdateTaskFailed()
    {
      var testFailureMessage = "failed message";
      var testTask = new MergeTask("testTaskId", "type", "description",
        new MergeMetadata(TileFormat.Jpeg, true, new TileBounds[0], new Source[0]),
        Status.PENDING, 0, "reason", 0, "testJobId", true, new DateTime(), new DateTime());

      this._taskUtilsMock.Setup(taskUtils => taskUtils.GetTask(It.IsAny<string>(), It.IsAny<string>())).Returns(testTask);
      this._taskExecutorMock.Setup(taskExecutor => taskExecutor.ExecuteTask(testTask, _taskUtilsMock.Object, It.IsAny<string?>())).Throws(new Exception(testFailureMessage));

      var testTaskRunner = new TaskRunner(_taskExecutorMock.Object, _jobUtilsMock.Object, _loggerMock.Object,
        _taskUtilsMock.Object, _heartbeatClientMock.Object, _metricsProviderMock.Object,
        _configurationManagerMock.Object);


      var testResultTask = testTaskRunner.FetchTask(new KeyValuePair<string, string>("testJobType", "testTaskType"));
      testTaskRunner.RunTask(testResultTask);

      Assert.AreEqual(testTask, testResultTask);
      _taskUtilsMock.Verify(taskUtils => taskUtils.UpdateReject(testTask.JobId, testTask.Id, testTask.Attempts, testFailureMessage, testTask.Resettable, It.IsAny<string?>()), Times.Once);
    }
  }
}
