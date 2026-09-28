Feature: Dispatching commands and queries
  Commands and queries are plain classes dispatched in process through Wolverine's
  IMessageBus. Their validators run as middleware, so an invalid request never reaches
  its handler.

  Scenario: A query returns a response
    When I dispatch a todo query for user "user-1"
    Then the dispatch succeeds

  Scenario: A command returns a response
    When I dispatch a create todo command titled "Write the docs" described as "Explain it"
    Then the dispatch succeeds

  Scenario Outline: A command missing a required field never reaches its handler
    When I dispatch a create todo command titled "<title>" described as "<description>"
    Then the dispatch is rejected as invalid
    And the validation failures mention "<field>"

    Examples:
      | title          | description | field       |
      |                | Explain it  | Title       |
      | Write the docs |             | Description |

  Scenario: A query missing a required field never reaches its handler
    When I dispatch a todo query for user ""
    Then the dispatch is rejected as invalid
    And the validation failures mention "User Id"

  Scenario: Validation failures are reported once each, not once per validator
    When I dispatch a create todo command titled "" described as ""
    Then the dispatch is rejected as invalid
    And there are exactly 2 validation failures

  Scenario: A rejected command fails immediately rather than being retried
    Given a create todo command has already been handled once
    When I dispatch a create todo command titled "" described as ""
    Then the dispatch is rejected as invalid
    And the rejection took less than 250 milliseconds
