Feature: Dispatching domain events
  Entities collect domain events and SaveChangesAsync publishes them after the write,
  in process, so their handlers run inside the same request.

  Scenario: A raised domain event reaches its handler
    Given a widget that raises a todo created event
    When I save changes
    Then the todo created event handler ran once

  Scenario: An event with no handler does not fail the write
    Given a widget that raises an event nothing handles
    When I save changes
    Then the save succeeds

  Scenario: Saving without any domain event raises nothing
    Given a widget that raises no events
    When I save changes
    Then the todo created event handler did not run
