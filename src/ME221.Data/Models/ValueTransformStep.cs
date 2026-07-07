namespace ME221.Data.Models;

public enum ValueTransformOperation
{
    Multiply = 0,
    Add = 1,
    Divide = 2,
    MinClamp = 3,
    MaxClamp = 4,
    InvertSign = 5,
}

public sealed class ValueTransformStep
{
    public ValueTransformOperation Operation { get; set; }
    public double Operand { get; set; }
}