using OpenAI.Decisions;
using Shared;

namespace DecisionApi.Cases;

/// <summary>
/// Image evidence. The user message gets an input_image part next to the text; the questions
/// do not change at all. Images go in as data URLs or public HTTPS URLs, never file paths.
/// Two calls: first decide what kind of question the user asked, then answer that kind.
/// Try with motor.jpg: "Does this asset have a nameplate?" / "Where is the nameplate?"
/// Try with animals.png: "Where is the elephant?" / "Is there a rabbit in the picture?"
/// </summary>
public static class Image
{
    public static async Task RunAsync(DecisionsClient client, string input, string imageFile = "motor.jpg")
    {
        Out.Step($"Image - answer a question about Data/{imageFile}");

        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Data", imageFile));
        string dataUrl = $"data:{MediaType(imageFile)};base64,{Convert.ToBase64String(bytes)}";

        DecisionsUserMessageItem evidence = new(
        [
            new DecisionsInputTextContentPart(input),
            new DecisionsInputImageContentPart(dataUrl) { ImageDetailLevel = DecisionsImageDetailLevel.Auto } // The API docs say "The model will determine the appropriate level of detail to use for the image." but don't say what that means. The other options are "low" and "high".
        ]);

        DecisionsPredicateQuestion kind = new(
            [new DecisionsInputTextInstructionsPart(
                "Does the user's text ask WHERE something is in the attached image? " +
                "'Where is the nameplate' is a location question. 'Is there a nameplate' is not.")])
        { Name = "asks_location" };

        DecisionResource first = await client.CreateDecisionAsync(new DecisionCreationOptions([evidence], Models.Decision, [kind]));
        bool asksLocation = first.Answers[0] is DecisionsPredicateAnswer a && a.Probability >= 0.5;
        first.PrintUsage();

        if (asksLocation)
        {
            // we have to manually put the locations it won't analyse the image for us, it just gives us the probabilities of the options we give it
            // maybe we could use a imageanalyser to give some locations and generate a schema for the model to choose from, but I'm not trying that here
            DecisionsChoiceQuestion position = new(
                [
                    new DecisionsChoice(new DecisionsChoiceValue("top")) { Description = "It is in the top half of the image" },
                    new DecisionsChoice(new DecisionsChoiceValue("bottom")) { Description = "It is in the bottom half of the image" },
                    new DecisionsChoice(new DecisionsChoiceValue("not_in_image")) { Description = "It is not in the image" }
                ],
                [new DecisionsInputTextInstructionsPart("Where in the image is the thing the user asks about?")])
            { Name = "position" };

            DecisionResource second = await client.CreateDecisionAsync(new DecisionCreationOptions([evidence], Models.Decision, [position]));
            if (second.Answers[0] is DecisionsChoiceAnswer c)
            {
                Console.WriteLine($"Position: {c.Choice.StringValue} ({c.Confidence:P0})");
            }
            second.PrintUsage();
        }
        else
        {
            DecisionsPredicateQuestion yesNo = new(
                [new DecisionsInputTextInstructionsPart("Based on the image, is the answer to the user's yes/no question 'yes'?")])
            { Name = "answer" };

            DecisionResource second = await client.CreateDecisionAsync(new DecisionCreationOptions([evidence], Models.Decision, [yesNo]));
            if (second.Answers[0] is DecisionsPredicateAnswer p)
            {
                Console.WriteLine($"Answer: {p.Probability:P0} yes");
            }
            second.PrintUsage();
        }
    }

    // The data URL must name the real format. The API accepts PNG, JPEG, WebP and GIF.
    private static string MediaType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        _ => throw new NotSupportedException($"Unsupported image type: {file}")
    };
}
