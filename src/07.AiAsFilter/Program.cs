// AI as a filter, not as a calculator.
//
// Naive approach: dump the whole dataset into the prompt and ask the question.
//   -> expensive (every row is input tokens), and the model miscounts.
//
// Filter approach: ask the model only to translate the question into a structured filter,
//   then run that filter in C#.
//   -> the dataset never leaves your process, the count is exact, tokens stay flat.
//
// This is structured output (RunAsync<T>) used as a query compiler.

using AiAsFilter;
using Microsoft.Agents.AI;
using OpenAI.Chat;
using Shared;
using System.Text.Json;

Out.Init("07 - AI as a Filter");

string json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Data", "workorders.json"));
List<WorkOrder> workOrders = JsonSerializer.Deserialize<List<WorkOrder>>(json,
    new JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })!;

Out.Note($"Loaded {workOrders.Count} work orders.");

const string question = "How many open work orders in Rotterdam cost more than 5000?";
int truth = workOrders.Count(w => w.Status == Status.Open && w.Site == "Rotterdam" && w.Cost > 5000);

Out.Separator();

// ---------------------------------------------------------------------------
Out.Step("A. Naive - send all 200 rows and ask the model to count");

AIAgent naiveAgent = Clients.Chat(Models.Standard)
    .AsAIAgent(instructions: "You analyse maintenance work orders. Answer with a number and a one-line explanation.");

AgentResponse naive = await naiveAgent.RunAsync(
    $"<workorders>{json}</workorders>\n\n{question}");

Console.WriteLine(naive); // this literally gave a wrong answer for me in the first run; why:)
naive.Usage.Print();

Out.Separator();

// ---------------------------------------------------------------------------
Out.Step("B. Filter - the model only builds the query, C# does the counting");

AIAgent filterAgent = Clients.Chat(Models.Standard)
    .AsAIAgent(instructions:
        "You translate questions about maintenance work orders into a set of filters. " +
        "Return only the filters needed. All filters are combined with AND.");

AgentResponse<WorkOrderFilter[]> filterResponse =
    await filterAgent.RunAsync<WorkOrderFilter[]>($"Build the filter for this question: {question}");

foreach (WorkOrderFilter filter in filterResponse.Result)
{
    Out.Note($"  filter: {filter.Field} {filter.Operation} {filter.Value}");
}

List<WorkOrder> filtered = Apply(workOrders, filterResponse.Result);
Console.WriteLine($"Answer: {filtered.Count}");
filterResponse.Usage.Print();

Out.Separator();
Out.Warn($"Real answer (plain LINQ): {truth}");

return;

static List<WorkOrder> Apply(List<WorkOrder> source, IEnumerable<WorkOrderFilter> filters)
{
    IEnumerable<WorkOrder> query = source;

    foreach (WorkOrderFilter filter in filters)
    {
        WorkOrderFilter f = filter;
        query = query.Where(w => Matches(w, f));
    }

    return query.ToList();
}

static bool Matches(WorkOrder workOrder, WorkOrderFilter filter)
{
    string actual = filter.Field switch
    {
        WorkOrderField.Asset => workOrder.Asset,
        WorkOrderField.Site => workOrder.Site,
        WorkOrderField.Priority => workOrder.Priority.ToString(),
        WorkOrderField.Status => workOrder.Status.ToString(),
        WorkOrderField.Cost => workOrder.Cost.ToString(),
        WorkOrderField.Year => workOrder.Year.ToString(),
        _ => string.Empty
    };

    bool numeric = filter.Field is WorkOrderField.Cost or WorkOrderField.Year;

    return filter.Operation switch
    {
        Operation.Equal => actual.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
        Operation.NotEqual => !actual.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
        Operation.Contains => actual.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
        Operation.GreaterThan => numeric && Compare(actual, filter.Value) > 0,
        Operation.LessThan => numeric && Compare(actual, filter.Value) < 0,
        _ => false
    };

    static int Compare(string left, string right)
        => int.TryParse(left, out int l) && int.TryParse(right, out int r) ? l.CompareTo(r) : 0;
}
