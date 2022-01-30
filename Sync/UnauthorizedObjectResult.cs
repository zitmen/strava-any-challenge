using Microsoft.AspNetCore.Mvc;

namespace Sync
{
    public class UnauthorizedObjectResult : ObjectResult
    {
        public UnauthorizedObjectResult(object value) : base(value)
        {
            StatusCode = 401;
        }
    }
}
