namespace VideoToAnimationTool.Core
{
    public sealed class ValidationResult
    {
        public ValidationResult(bool isValid, string[] errors)
        {
            IsValid = isValid;
            Errors = errors ?? new string[0];
        }

        public bool IsValid { get; private set; }
        public string[] Errors { get; private set; }
    }
}
