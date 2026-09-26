Feature: Broker routing is opt-in
  Wolverine is both the mediator and the message bus, so every command, query and domain
  event is a message to it. Only the contracts declared as broker contracts are routed over
  RabbitMQ; everything else stays on an in-process queue.

  Scenario Outline: A message that is not a broker contract stays in process
    When I inspect the routing for a <message>
    Then it is not routed to the broker
    And it is routed in process

    Examples:
      | message             |
      | create todo command |
      | todo query          |
      | todo created event  |

  Scenario: A declared broker contract is routed to the broker
    When I inspect the routing for a todo message
    Then it is routed to the broker
