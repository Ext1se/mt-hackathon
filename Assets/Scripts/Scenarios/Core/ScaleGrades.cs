namespace Game.Scenarios.Core
{
    /// <summary>Grade thresholds from Docs/Scenarios_RU.md: 80+, 60-79, 40-59, below 40.</summary>
    public static class ScaleGrades
    {
        public static ScaleGrade From(int value)
        {
            if (value >= 80)
            {
                return ScaleGrade.Excellent;
            }

            if (value >= 60)
            {
                return ScaleGrade.Good;
            }

            return value >= 40 ? ScaleGrade.Satisfactory : ScaleGrade.Fail;
        }
    }
}
