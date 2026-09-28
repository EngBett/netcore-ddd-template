using System.Net.Http.Json;
using System.Text.Json;
using Reqnroll;
using Template.Tests.Support;
using Xunit;

namespace Template.Tests.Steps;

[Binding]
public class HttpSteps
{
    // Unique per scenario, so the fault it arranges cannot leak into a parallel scenario.
    private readonly string _userId = $"http-{Guid.NewGuid():N}";
    private HttpResponseMessage? _response;
    private JsonElement _body;

    [Given("the todo query will fail validation with {string}")]
    public void GivenTheTodoQueryWillFailValidation(string message) =>
        QueryFaults.Arrange(_userId, QueryFault.ValidationFailure, message);

    [Given("the todo query will break a business rule with {string}")]
    public void GivenTheTodoQueryWillBreakABusinessRule(string message) =>
        QueryFaults.Arrange(_userId, QueryFault.DomainRuleViolation, message);

    [Given("the todo query will report not found with {string}")]
    public void GivenTheTodoQueryWillReportNotFound(string message) =>
        QueryFaults.Arrange(_userId, QueryFault.NotFound, message);

    [Given("the todo query will throw an unexpected exception")]
    public void GivenTheTodoQueryWillThrowAnUnexpectedException() =>
        QueryFaults.Arrange(_userId, QueryFault.UnexpectedException, "Something the handler did not expect");

    [When("I request the todos endpoint")]
    public async Task WhenIRequestTheTodosEndpoint()
    {
        using var client = ApiHost.CreateClient();
        _response = await client.GetAsync(new Uri($"/api/v1/test?userId={_userId}", UriKind.Relative));

        _body = await _response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [AfterScenario]
    public void ClearFault()
    {
        QueryFaults.Clear(_userId);
        _response?.Dispose();
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int expected) =>
        Assert.Equal(expected, (int)_response!.StatusCode);

    [Then("the response message is {string}")]
    public void ThenTheResponseMessageIs(string expected) =>
        Assert.Equal(expected, _body.GetProperty("message").GetString());

    [Then("the response message carries an error code")]
    public void ThenTheResponseMessageCarriesAnErrorCode() =>
        // The same id is on the logged exception, which is how a reported failure is traced.
        Assert.Matches(@"Error Code: [0-9a-f-]{36}$", _body.GetProperty("message").GetString());

    [Then("the response errors include {string}")]
    public void ThenTheResponseErrorsInclude(string expected) =>
        Assert.Contains(expected, _body.GetProperty("errors").EnumerateArray().Select(error => error.GetString()));
}
