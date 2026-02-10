using MergerLogic.Clients;
using MergerLogic.DataTypes;
using MergerLogic.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.IO;

namespace MergerLogicUnitTests.Utils
{
    [TestClass]
    [TestCategory("unit")]
    [TestCategory("http")]
    [TestCategory("HttpUtils")]
    [DeploymentItem(@"../../../Utils/TestData")]
    public class HttpSourceUtilsTest
    {
        #region mocks
        private MockRepository _repository;
        private Mock<IHttpRequestUtils> _httpRequestUtilsMock;
        private Mock<IPathPatternUtils> _pathPatternUtilsMock;
        private Mock<IGeoUtils> _geoUtilsMock;
        private byte[] _jpegImageData;
        #endregion

        [TestInitialize]
        public void beforeEach()
        {
            this._repository = new MockRepository(MockBehavior.Strict);
            this._httpRequestUtilsMock = this._repository.Create<IHttpRequestUtils>();
            this._pathPatternUtilsMock = this._repository.Create<IPathPatternUtils>();
            this._geoUtilsMock = this._repository.Create<IGeoUtils>();

            this._jpegImageData = File.ReadAllBytes("no_transparency.jpeg");
        }

        #region GetTile

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public void GetTile(bool useCoords, bool returnsNull)
        {
            Coord cords = new Coord(1, 2, 3);
            byte[] data = this._jpegImageData;

            this._pathPatternUtilsMock.Setup(util => util.RenderUrlTemplate(cords.X, cords.Y, cords.Z))
                .Returns("testPath");
            this._httpRequestUtilsMock.Setup(util => util.GetData("testPath", true))
                .Returns(returnsNull ? null : data);

            var httpSourceUtils = new HttpSourceClient("http://testPath", this._httpRequestUtilsMock.Object,
                this._pathPatternUtilsMock.Object, this._geoUtilsMock.Object);

            var res = useCoords ? httpSourceUtils.GetTile(cords, null) : httpSourceUtils.GetTile(cords.Z, cords.X, cords.Y);
            if (returnsNull)
            {
                Assert.IsNull(res);
            }
            else
            {
                Assert.AreEqual(cords.X, res.X);
                Assert.AreEqual(cords.Y, res.Y);
                Assert.AreEqual(cords.Z, res.Z);
                CollectionAssert.AreEqual(data, res.GetImageBytes());
            }
            this._repository.VerifyAll();
        }

        #endregion

        #region TileExists

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TileExists(bool exist)
        {
            Coord cords = new Coord(1, 2, 3);
            byte[] data = this._jpegImageData;

            this._pathPatternUtilsMock.Setup(util => util.RenderUrlTemplate(cords.X, cords.Y, cords.Z))
                .Returns("testPath");
            this._httpRequestUtilsMock.Setup(util => util.GetData("testPath", true))
                .Returns(exist ? data : null);

            var httpSourceUtils = new HttpSourceClient("http://testPath", this._httpRequestUtilsMock.Object,
                this._pathPatternUtilsMock.Object, this._geoUtilsMock.Object);

            var res = httpSourceUtils.TileExists(cords.Z, cords.X, cords.Y);

            Assert.AreEqual(exist, res);
            this._repository.VerifyAll();
        }

        #endregion

    }
}
