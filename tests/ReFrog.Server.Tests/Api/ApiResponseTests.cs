using System.Text.Json;
using ReFrog.Contracts.Api;

namespace ReFrog.Server.Tests.Api;

[TestClass]
public sealed class ApiResponseTests
{
    [TestMethod]
    public void SuccessHasDataAndNoError()
    {
        var response = ApiResponse.Success("recorded");

        Assert.AreEqual("recorded", response.Data);
        Assert.IsNull(response.Error);
    }

    [TestMethod]
    public void FailureHasErrorAndNoData()
    {
        var error = new ApiError(
            ApiErrorCategory.Validation,
            "VALIDATION_FAILED",
            "Correct the invalid fields.",
            new Dictionary<string, string> { ["email"] = "Enter a valid email address." });

        var response = ApiResponse.Failure<string>(error);

        Assert.IsNull(response.Data);
        Assert.AreSame(error, response.Error);
    }

    [TestMethod]
    public void FactoriesRejectNullPayloads()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => ApiResponse.Success<string>(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => ApiResponse.Failure<string>(null!));
    }

    [TestMethod]
    public void ErrorCategoryUsesTheArchitectureContractName()
    {
        var response = ApiResponse.Failure<string>(new ApiError(
            ApiErrorCategory.Unavailable,
            "EMAIL_NOT_SENT",
            "Email could not be sent."));

        var json = JsonSerializer.Serialize(response);

        StringAssert.Contains(json, "\"Category\":\"UNAVAILABLE\"");
    }
}
