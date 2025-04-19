using FuseDrill;

namespace Tests
{
    public class Tests
    {
        [Fact]
        public async Task TestExampleUsingNugetLib()
        {
            //if you are using top-level statements in web api you need to add :
            //public partial class Program { } in Program.cs class

            var fuzzer = new ApiFuzzerWithVerifier<Program>();
            await fuzzer.TestWholeApi();
        }

    }
}