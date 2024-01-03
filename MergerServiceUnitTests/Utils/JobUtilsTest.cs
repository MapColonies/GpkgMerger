using MergerLogic.Clients;
using MergerLogic.Utils;
using MergerService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Diagnostics;

namespace MergerLogicUnitTests.Utils
{
  [TestClass]
  [TestCategory("unit")]
  [TestCategory("utils")]
  public class JobUtilsTest
  {
    #region mocks

    private MockRepository _mockRepository;

    private Mock<IHttpRequestUtils> _httpClientMock;
    private Mock<IConfigurationManager> _configurationManagerMock;
    private Mock<ILogger<JobUtils>> _jobUtilsLoggerMock;

    private ActivitySource _testActivitySource;
    private string _testJobManagerUrl = "http://testJobManagerUrl";

    #endregion

    [TestInitialize]
    public void BeforeEach()
    {
      this._mockRepository = new MockRepository(MockBehavior.Loose);

      this._httpClientMock = this._mockRepository.Create<IHttpRequestUtils>();
      this._configurationManagerMock = this._mockRepository.Create<IConfigurationManager>();
      this._jobUtilsLoggerMock = this._mockRepository.Create<ILogger<JobUtils>>();

      this._testActivitySource = new ActivitySource("test");

      this._configurationManagerMock.Setup(configManager => configManager.GetConfiguration("TASK", "jobManagerUrl")).Returns(this._testJobManagerUrl);
    }

    [TestMethod]
    public void WhenGettingJobById_ShouldUseCorrectHttpUri()
    {
      var testJobId = "testJobId";
      var testJobUtils = new JobUtils(_configurationManagerMock.Object, _httpClientMock.Object, _jobUtilsLoggerMock.Object,
        _testActivitySource);

      testJobUtils.GetJob(testJobId);

      var expectedUrl = $"{this._testJobManagerUrl.ToLower()}/jobs/{testJobId}?shouldReturnTasks=false";
      this._httpClientMock.Verify(httpClient => httpClient.GetDataString(expectedUrl, It.IsAny<bool>()), Times.Once);
    }
  }
}
