using System;
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

            foreach (var currentFile in Directory.GetFiles(path, "*.asm"))
            {
                Console.WriteLine("Testing " + currentFile);


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
            lc3a.Assemble(new List<string>()
            {
                "TRAP -128",
                "TRAP 127",
                "TRAP x7F",
                "TRAP 0"
            });
            Assert.IsEmpty(lc3a.Errors);


            lc3a.Assemble(new List<string>()
            {
                "TRAP -129",
                "TRAP 128"
            });
            Assert.AreEqual(lc3a.Errors[0].line, 0);
            Assert.AreEqual(lc3a.Errors[1].line, 1);
            Assert.AreEqual(lc3a.Errors.Count, 2);




            // Labels
        }

        [Test]
        public void TestAssemblerHelperMethods()
        {
            Assert.IsTrue(lc3a.IsValidOffset(0, 4));
            Assert.IsTrue(lc3a.IsValidOffset(7, 4));
            Assert.IsTrue(lc3a.IsValidOffset(-8, 4));

            Assert.IsFalse(lc3a.IsValidOffset(8, 4));
            Assert.IsFalse(lc3a.IsValidOffset(-9, 4));
        }
    }
}
