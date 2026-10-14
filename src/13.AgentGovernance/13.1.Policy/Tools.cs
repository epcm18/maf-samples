using System.ComponentModel;

namespace AgtPolicy;

public static class Tools
{
    [Description("Gets the ward a patient is admitted to.")]
    public static string GetPatientWard(string patientName) => patientName switch
    {
        "John Smith" => "P-001: John Smith, Male Ward.",
        "Mary Perera" => "P-002: Mary Perera, Female Ward.",
        _ => $"No patient {patientName}."
    };

    [Description("Gets the latest medical report of a ward.")]
    public static string GetMedicalReport(string ward)
    {
        if (string.IsNullOrEmpty(ward))
            return "Invalid ward specified.";

        if (ward == "Male Ward")
            return "Latest medical report for Male Ward: Patient John Smith - Blood Pressure: 120/80, Heart Rate: 72 bpm.";
        else if (ward == "Female Ward")
            return "Latest medical report for Female Ward: Patient Mary Perera - Blood Pressure: 110/70, Heart Rate: 75 bpm.";
        else
            return $"No medical report available for {ward}.";
    }
}
