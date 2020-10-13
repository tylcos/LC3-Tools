using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace LC3
{
    class LC3Assembler
    {
        public List<Instruction>               Instructions { get; private set; }
        public List<(int line, string msg)>    Errors       { get; private set; }
        public Dictionary<string, int>         Labels       { get; private set; }

        private record LabelRefrence(int Line, int OffsetSize, string Label);
        private List<LabelRefrence> LabelRefrences;

        private int lineNum = -1;


        public LC3Assembler()
        {
        }
            

        private static readonly Dictionary<string, (int argumentCount, int opcode)> InstructionInfo = new()
        {
            ["add"]  = (4, 0x1000),
            ["and"]  = (4, 0x5000),
            ["br"]   = (3, 0x0000),
            ["jmp"]  = (2, 0xC000),
            ["jsr"]  = (2, 0x4000),
            ["jsrr"] = (2, 0x4000),
            ["ld"]   = (3, 0x2000),
            ["ldi"]  = (3, 0xA000),
            ["ldr"]  = (4, 0x6000),
            ["lea"]  = (3, 0xE000),
            ["not"]  = (3, 0x9000),
            ["ret"]  = (1, 0xC000),
            ["rti"]  = (1, 0x8000),
            ["st"]   = (3, 0x3000),
            ["sti"]  = (3, 0xB000),
            ["str"]  = (4, 0x7000),
            ["trap"] = (2, 0xF000),

            // Pseudo-Ops
            ["halt"] = (1, 0xF000)
        };


        public List<Instruction> Assemble(string path) => Assemble(File.ReadLines(path));


        public List<Instruction> Assemble(IEnumerable<string> lines)
        {
            Instructions   = new(32);
            Errors         = new();
            Labels         = new();
            LabelRefrences = new();

            lineNum = -1; 

            string[] parts = null;                                     // Current line split on delimiter
            string opcode = "";                                        // Current opcode
            (int argumentCount, int opcode) instructionInfo = default; // Info about current opcode


            foreach (string line in lines)
            {
                lineNum++;

                if (!ParseLine(line))
                    continue;


                int CurrentInstruction = 0;
                switch (opcode)
                {
                    case "add":  // ADD r0 r0 r0
                    case "and":  // AND r0 r0 0
                        CurrentInstruction = parts[3][0] == 'r'
                            ? instructionInfo.opcode + Register(1, 9) + Register(2, 6) + Register(3, 0)
                            : instructionInfo.opcode + Register(1, 9) + Register(2, 6) + 0x0020 + Offset(3, 5);
                        break;
                    case "not":  // NOT r0 r0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Register(2, 6) + 0x003F;
                        break;


                    case "br":
                        int conditions = 0x0E00;
                        if (!parts[1].Contains("n")) conditions &= 0xF7FF;
                        if (!parts[1].Contains("z")) conditions &= 0xFBFF;
                        if (!parts[1].Contains("p")) conditions &= 0xFDFF;

                        CurrentInstruction = instructionInfo.opcode + conditions + Offset(2, 9);
                        break;
                    case "jmp":  // JMP r0
                    case "jsrr": // JSRR r0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 6);
                        break;
                    case "ret":  // RET
                        CurrentInstruction = instructionInfo.opcode + 0x01C0;
                        break;
                    case "rti":  // RTI
                        CurrentInstruction = instructionInfo.opcode;
                        break;
                    case "jsr":  // JSR 0
                        CurrentInstruction = instructionInfo.opcode + 0x0800 + Offset(1, 11);
                        break;
                    case "trap": // TRAP 0
                        CurrentInstruction = instructionInfo.opcode + Offset(1, 8);
                        break;


                    case "ld":   // LD r0 0
                    case "ldi":  // LDI r0 0
                    case "lda":  // LDA r0 0
                    case "st":   // ST r0 0
                    case "sti":  // STI r0 0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Offset(2, 9);
                        break;
                    case "ldr":  // LDR r0 r0 0
                    case "str":  // STR r0 r0 0
                        CurrentInstruction = instructionInfo.opcode + Register(1, 9) + Register(2, 6) + Offset(3, 6);
                        break;


                    case "halt":  // HALT
                        CurrentInstruction = instructionInfo.opcode + 0x0025;
                        break;
                }


                Instructions.Add(new Instruction(CurrentInstruction));
            }


            AssignLabelRefrences();

            return Instructions;


            int currentPC() => Instructions.Count;

            int Register(int partNum, int offset = 0)
            {
                int reg = parts[partNum][1] - '0';

                if (SyntaxErrorIf(reg < 0 && reg > 7, "Invalid register number: " + parts[partNum]))
                    return 0;

                return reg << offset;
            }

            int Offset(int partNum, int offsetSize)
            {
                string offsetString = parts[partNum];
                string label = "";


                int offset = 0;
                // Hex
                if (offsetString[0] == 'x')
                {
                    bool validOffset = int.TryParse(offsetString[1..], NumberStyles.HexNumber, null, out offset);
                    SyntaxErrorIf(!validOffset, "Cannot parse hex offset: " + offsetString);
                }
                // Decimal
                else if (char.IsDigit(offsetString[0]) || offsetString[0] == '-') 
                {
                    bool validOffset = int.TryParse(parts[partNum], out offset);
                    SyntaxErrorIf(!validOffset, "Cannot parse offset: " + offsetString);
                }
                // Known label 
                else if (Labels.TryGetValue(offsetString, out int labelOffset))
                {
                    offset = labelOffset - currentPC() - 1;
                    label = offsetString;
                }
                // Unknown label
                else
                    LabelRefrences.Add(new LabelRefrence(currentPC(), offsetSize, offsetString));


                return IsValidOffset(offset, offsetSize, label) ? offset & ((1 << offsetSize) - 1) : 0;
            }

            // Overwrites parts, opcode, instructionInfo
            bool ParseLine(string line)
            {
                // Could make this far more efficient
                string trimmedLine = line.Trim().ToLower().Split(new[] { ';' }, 1)[0];
                if (trimmedLine.Length == 0)
                    return false;
                parts = trimmedLine.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                opcode = parts[0];


                // Deal with BR instruction, BRnzp 0 -> BR nzp 0
                if (opcode[0..2] == "br")
                {
                    parts = opcode.Length == 2
                        ? new string[] { "br", "nzp", parts[1] }
                        : new string[] { "br", opcode[2..], parts[1] };
                    opcode = "br";
                }


                // Check for label before instruction
                if (IsValidLabel(opcode, false))
                {
                    Labels.Add(opcode, currentPC());

                    if (parts.Length > 1) // LABEL BR 0
                    {
                        parts = parts[1..];
                        opcode = parts[0];
                    }
                    else                  // LABEL
                        return false;
                }

                // Check for valid instruction
                if (SyntaxErrorIf(!InstructionInfo.TryGetValue(opcode, out instructionInfo), 
                        $"Instruction '{opcode}' not recognized.")
                    || SyntaxErrorIf(parts.Length != instructionInfo.argumentCount, 
                        $"Invalid {opcode.ToUpper()} instruction, needs {instructionInfo.argumentCount} arguments."))
                    return false;

                return true;
            }
        }


        private void AssignLabelRefrences()
        {
            foreach ((int line, int offsetSize, string label) in LabelRefrences)
            {
                if (SyntaxErrorIf(!Labels.TryGetValue(label, out int labelOffset), $"Label '{label}' cannot be found."))
                    continue;

                int offset = labelOffset - line - 1;
                int sizeMask = (1 << offsetSize) - 1;

                if (!IsValidOffset(offset, offsetSize, label))
                    continue;

                Instructions[line] = new Instruction(Instructions[line].Bits | (offset & sizeMask));
            }
        }




        /// <summary>
        ///     Adds error and returns True if 'check' is True.
        /// </summary>
        /// <param name="check">True for bad syntax. </param>
        /// <param name="errorMsg">Error message to be added if assertion is False. </param>
        /// <returns>True if error was added</returns>
        private bool SyntaxErrorIf(bool check, string errorMsg)
        {
            if (check)
                Errors.Add((lineNum, errorMsg));

            return check;
        }


        private bool IsValidLabel(string label, bool isDeclared)
        {
            return label.Length > 0 && label[0] != 'x' && !char.IsDigit(label[0])
                && !InstructionInfo.ContainsKey(label)
                && (isDeclared == Labels.ContainsKey(label));
        }

        public bool IsValidOffset(int offset, int offsetSize, string label = "")
        {
            int sizeMask = 1 << (offsetSize - 1);
            bool valid = offset < 0 ? -offset <= sizeMask : offset < sizeMask;


            int limit = offset < 0 ? -sizeMask : sizeMask - 1;
            string errorMsg = label == ""
                ? $"Offset '{offset}' cannot fit within {offsetSize} bits, the limit is {limit}."
                : $"Offset for label '{label}' with value {offset} cannot fit within {offsetSize} bits, the limit is {limit}.";
            SyntaxErrorIf(!valid, errorMsg);


            return valid;
        }
    }
}
