public class RiddleImportData
{
    public string id;

    public string category;

    public RiddleDifficulty difficulty;

    public string answer;

    public string[] acceptedAnswers;

    public string[] hints;

    private RiddleImportData CreateImportData(string[] row)
    {
        RiddleImportData data = new RiddleImportData();

        data.id = row[0];
        data.category = row[1];

        switch (row[2])
        {
            case "Easy":
                data.difficulty = RiddleDifficulty.Easy;
                break;

            case "Medium":
                data.difficulty = RiddleDifficulty.Medium;
                break;

            default:
                data.difficulty = RiddleDifficulty.Hard;
                break;
        }

        data.answer = row[3];

        data.acceptedAnswers = row[4].Split('|');

        data.hints = new string[]
        {
        row[5],
        row[6],
        row[7],
        row[8],
        row[9]
        };

        return data;
    }
}
