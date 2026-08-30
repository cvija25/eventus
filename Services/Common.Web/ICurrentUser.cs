namespace Common.Web;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
