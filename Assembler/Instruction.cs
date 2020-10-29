using System;

using LC3.Compiler;

namespace LC3
{
    record Instruction
    {
        public short Bits;


        public Instruction(int bits)
        {
            Bits = (short)bits;
        }


        public int Opcode => (Bits & 0xF000) >> 12;

        public int DR => (Bits & 0x0E00) >> 9;
        public int SR1 => (Bits & 0x01C0) >> 6;
        public int SR2 => (Bits & 0x0007);


        public bool Get(int pos) => (Bits & (1 << pos)) != 0;
        public int GetRange(int length) => Bits & ((1 << length) - 1);


        public static Instruction Empty = new Instruction(0);


        public override string ToString() => Bits.ToString("X4");
    }
}
