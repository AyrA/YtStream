namespace YtStream
{
    public interface IValidateable
    {
        bool IsValid();

        string[] GetValidationMessages();
    }
}
