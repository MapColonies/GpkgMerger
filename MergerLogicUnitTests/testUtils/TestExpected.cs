namespace MergerLogicUnitTests.testUtils
{
    public class TestExpected<TestDataType, ExpectedDataType>
    {
        public TestDataType TestData { get; set; }
        public ExpectedDataType ExpectedData { get; set; }

        public TestExpected(TestDataType testData, ExpectedDataType expectedData)
        {
            this.TestData = testData;
            this.ExpectedData = expectedData;
        }
    }
}
