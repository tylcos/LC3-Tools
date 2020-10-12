using System.Collections.Generic;
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
        public void TestAssemblerData()
        {
            string path = TestContext.CurrentContext.TestDirectory + @"\Tests\Data\";

            foreach (var currentFile in Directory.EnumerateFiles(path, "*.asm"))
            {
                lc3a.Errors.Clear();
                var program = lc3a.Assemble(currentFile).Select(i => i.ToString()).ToArray();


                Assert.IsEmpty(lc3a.Errors);

                var expectedPath = path + Path.GetFileNameWithoutExtension(currentFile) + ".dat";
                if (File.Exists(expectedPath))
                    Assert.AreEqual(File.ReadAllLines(expectedPath), program);
            }
        }

        [Test]
        public void TestAssemblerSpecific()
        {
            // Offsets
            lc3a.Errors.Clear();
            lc3a.Assemble(new List<string>()
            {
                "TRAP -128",
                "TRAP 127",
                "TRAP x7F",
                "TRAP 0"
            });
            Assert.IsEmpty(lc3a.Errors);


            lc3a.Errors.Clear();
            lc3a.Assemble(new List<string>()
            {
                "TRAP -129",
                "TRAP 128"
            });
            Assert.AreEqual(lc3a.Errors[0], (0, "Offset '-129' to large to fit in 8 bits."));
            Assert.AreEqual(lc3a.Errors[1], (1, "Offset '128' to large to fit in 8 bits."));
            Assert.IsTrue(lc3a.Errors.Count == 2);
        }
    }
}
