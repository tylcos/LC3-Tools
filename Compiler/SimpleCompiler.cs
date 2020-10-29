using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace LC3.Compiler
{
    class SimpleCompiler
    {
        public SimpleCompiler()
        {
            var conditional = "r0 < r1".Split(" ").Select(l => new Lexeme(l)).ToList();

            CompilerHelpers.CreateConditional(conditional, null).ForEach(l => Console.WriteLine(l));
        }
    }
}
