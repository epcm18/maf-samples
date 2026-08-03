using System.ComponentModel;

namespace AiAsFilter;

public record WorkOrder(
    int Id,
    string Asset,
    string Site,
    Priority Priority,
    Status Status,
    int Cost,
    int Year);

public enum Priority { Low, Medium, High, Critical }

public enum Status { Open, InProgress, Completed, Cancelled }

// This is what the LLM produces instead of an answer: a machine-readable filter.
// The [Description] attributes end up in the JSON schema the model is constrained to,
// so they are the cheapest place to teach it what each field means.
public class WorkOrderFilter
{
    [Description("Which field of the work order to compare.")]
    public required WorkOrderField Field { get; set; }

    [Description("How to compare the field against Value.")]
    public required Operation Operation { get; set; }

    [Description("The value to compare against, always as a string. For enums use the enum name.")]
    public required string Value { get; set; }
}

public enum WorkOrderField { Asset, Site, Priority, Status, Cost, Year }

public enum Operation { Equal, NotEqual, Contains, GreaterThan, LessThan }
