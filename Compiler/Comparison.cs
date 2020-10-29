using System;

namespace LC3.Compiler
{
    class Comparison
    {
        public int Type { get; init; }


        private readonly string[] operators = { "==", "<", ">", "<=", ">=", "!=" };
        private readonly string[] codes     = { "z",  "n", "p", "nz", "pz", "np" };


        public Comparison(Lexeme lexeme)
        {
            Type = Array.IndexOf(operators, lexeme.Value);
        }

        private Comparison(int type) => Type = type;




        public static Comparison operator !(Comparison a) => new Comparison(Math.Abs(5 - a.Type));

        public override string ToString() => codes[Type];




        public enum ComparisonType
        {
            Equals,
            LessThan,
            GreaterThan,
            LessThanEquals,
            GreaterThanEquals
        }
    }
}