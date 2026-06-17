// ReSharper disable InconsistentNaming
namespace VectraLang.Bytecode;

public enum OpCode : byte
{
    // Stack Ops
    PUSH_INT = 0X00,        // PUSH_INT <constant_pool_index>
    PUSH_FLOAT = 0X01,      // PUSH_FLOAT <constant_pool_index>
    PUSH_BOOL = 0X02,       // PUSH_BOOL <val: 0 = false, 1 = true>
    PUSH_STRING = 0X03,     // PUSH_STRING <constant_pool_index>
    PUSH_NULL = 0X04,       // PUSH_NULL
    POP = 0X05,             // POP
    DUP = 0X06,             // DUP
    // Variable Ops
    LOAD_LOCAL = 0X10,      // LOAD_LOCAL <index>
    STORE_LOCAL = 0X11,     // STORE_LOCAL <index>
    ALLOC = 0X12,           // ALLOC <type_index>
    GET_FIELD = 0X13,       // GET_FIELD <field_index>
    SET_FIELD = 0X14,       // SET_FIELD <field_index>
    GET_STATIC = 0X15,      // GET_STATIC <type_index> <field_index>
    SET_STATIC = 0X16,      // SET_STATIC <type_index> <field_index>
    // Arithmetic
    ADD = 0X20,             // ADD
    SUB = 0X21,             // SUB
    MUL = 0X22,             // MUL
    DIV = 0X23,             // DIV
    MOD = 0X24,             // MOD
    NEG = 0X25,             // NEG
    // Comparison
    EQ = 0X30,              // EQ
    NEQ = 0X31,             // NEQ
    LT = 0X32,              // LT
    LTE = 0X33,             // LTE
    GT = 0X34,              // GT
    GTE = 0X35,             // GTE
    // Logical
    AND = 0X40,             // AND
    OR = 0X41,              // OR
    NOT = 0X42,             // NOT
    // String
    CONCAT = 0X50,          // CONCAT
    // Control flow
    JMP = 0X60,             // JMP <offset> (absolute)
    JMP_TRUE = 0X61,        // JMP_TRUE <offset> (absolute)
    JMP_FALSE = 0X62,       // JMP_FALSE <offset> (absolute)
    // Function calls
    CALL = 0X70,            // CALL <function_index> <arg_count>
    CALL_EXTERN = 0X71,     // CALL_EXTERN <module_index> <function_index> <arg_count>
    RET = 0X72,             // RET
    RET_VAL = 0X73          // RET_VAL
}