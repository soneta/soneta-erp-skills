using Soneta.Types.DynamicApi;

namespace {Namespace}.Interfaces;

public interface I{ProjectName}Api
{
    [DynamicApiMethod(HttpMethods.POST, MediaType = "application/json")]
    {MethodName}Response {MethodName}({MethodName}Request request);
}
