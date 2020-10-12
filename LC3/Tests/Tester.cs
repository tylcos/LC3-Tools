using System.IO;
using System.Linq;

using NUnit.Framework;

namespace LC3
{
    [TestFixture]
    class Tester
    {
        private readonly LC3Assembler lc3a = new LC3Assembler();


        [Test]
        public void TestAssembler()
        {
            string path = TestContext.CurrentContext.TestDirectory + @"\Tests\Data\";

            foreach (var currentFile in Directory.EnumerateFiles(path, "*.asm"))
            {
                var program = lc3a.Assemble(currentFile).Select(i => i.ToString()).ToArray();

                var expectedPath = path + Path.GetFileNameWithoutExtension(currentFile) + ".dat";
                Assert.AreEqual(File.ReadAllLines(expectedPath), program);
            }
        }
    }
}
