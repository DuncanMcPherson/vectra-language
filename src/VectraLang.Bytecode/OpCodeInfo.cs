namespace VectraLang.Bytecode;

public static class OpCodeInfo
{
    private static readonly Dictionary<OpCode, int> OperandCounts = new()
    {
        { OpCode.PUSH_INT,    1 },
        { OpCode.PUSH_FLOAT,  1 },
        { OpCode.PUSH_BOOL,   1 },
        { OpCode.PUSH_STRING, 1 },
        { OpCode.PUSH_NULL,   0 },
        { OpCode.POP,         0 },
        { OpCode.DUP,         0 },
        { OpCode.LOAD_LOCAL,  1 },
        { OpCode.STORE_LOCAL, 1 },
        { OpCode.ALLOC,       1 },
        { OpCode.GET_FIELD,   1 },
        { OpCode.SET_FIELD,   1 },
        { OpCode.GET_STATIC,  2 },
        { OpCode.SET_STATIC,  2 },
        { OpCode.ADD,         0 },
        { OpCode.SUB,         0 },
        { OpCode.MUL,         0 },
        { OpCode.DIV,         0 },
        { OpCode.MOD,         0 },
        { OpCode.NEG,         0 },
        { OpCode.EQ,          0 },
        { OpCode.NEQ,         0 },
        { OpCode.LT,          0 },
        { OpCode.LTE,         0 },
        { OpCode.GT,          0 },
        { OpCode.GTE,         0 },
        { OpCode.AND,         0 },
        { OpCode.OR,          0 },
        { OpCode.NOT,         0 },
        { OpCode.CONCAT,      0 },
        { OpCode.JMP,         1 },
        { OpCode.JMP_TRUE,    1 },
        { OpCode.JMP_FALSE,   1 },
        { OpCode.CALL,        2 },
        { OpCode.CALL_EXTERN, 3 },
        { OpCode.RET,         0 },
        { OpCode.RET_VAL,     0 },
    };

    public static int GetOperandCount(OpCode op) => OperandCounts[op];
    
    private static readonly Dictionary<OpCode, string[]> OperandNames = new()
    {
        { OpCode.PUSH_INT,    ["const_idx"] },
        { OpCode.PUSH_FLOAT,  ["const_idx"] },
        { OpCode.PUSH_BOOL,   ["value"] },
        { OpCode.PUSH_STRING, ["const_idx"] },
        { OpCode.LOAD_LOCAL,  ["slot"] },
        { OpCode.STORE_LOCAL, ["slot"] },
        { OpCode.ALLOC,       ["type_idx"] },
        { OpCode.GET_FIELD,   ["field_idx"] },
        { OpCode.SET_FIELD,   ["field_idx"] },
        { OpCode.GET_STATIC,  ["type_idx", "field_idx"] },
        { OpCode.SET_STATIC,  ["type_idx", "field_idx"] },
        { OpCode.JMP,         ["target"] },
        { OpCode.JMP_TRUE,    ["target"] },
        { OpCode.JMP_FALSE,   ["target"] },
        { OpCode.CALL,        ["method_idx", "arg_count"] },
        { OpCode.CALL_EXTERN, ["module_idx", "method_idx", "arg_count"] },
    };

    public static string[] GetOperandNames(OpCode op) =>
        OperandNames.TryGetValue(op, out var names) ? names : [];
}