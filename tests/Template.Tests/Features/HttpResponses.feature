Feature: What a client sees over HTTP
  Handlers report outcomes through ApiResponse codes and exceptions. The API turns each into
  the same status code and ApiResponse body whichever API style the service was scaffolded
  with: controllers through GlobalExceptionFilter and BaseController, Minimal APIs and
  FastEndpoints through ExceptionResponseMiddleware and ApiResponseResults.

  Scenario: A successful query is a 200
    When I request the todos endpoint
    Then the response status is 200

  Scenario: A validation failure is a 400 listing the errors
    Given the todo query will fail validation with "'User Id' must not be empty."
    When I request the todos endpoint
    Then the response status is 400
    And the response message is "'User Id' must not be empty."
    And the response errors include "'User Id' must not be empty."

  Scenario: A broken business rule is a 400 with its message
    Given the todo query will break a business rule with "Todo lists are closed"
    When I request the todos endpoint
    Then the response status is 400
    And the response message is "Todo lists are closed"

  Scenario: A not-found result is a 404
    Given the todo query will report not found with "No todos for this user"
    When I request the todos endpoint
    Then the response status is 404
    And the response message is "No todos for this user"

  Scenario: An unexpected exception is a 500
    Given the todo query will throw an unexpected exception
    When I request the todos endpoint
    Then the response status is 500
    And the response message carries an error code
