# P8 internal provider cost report

The official live profile is `openai-gpt-5-mini-2025-08-07-v1`. It uses the
OpenAI Responses API with strict structured output, a pinned model snapshot,
2,200 maximum assembled input tokens, 180 maximum output tokens, one repair,
and a 25-second independent timeout. The adapter follows the current
[Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs)
and the pinned model supports both Responses and Structured Outputs according
to the [model reference](https://developers.openai.com/api/docs/models/gpt-5-mini).

The checked-in price table `openai-2026-08-09` records $0.25 per million input
tokens and $2.00 per million output tokens. At the configured hard limits:

- one maximum attempt reserves 910 cost micros ($0.000910);
- two simultaneous agents with one repair each reserve 3,640 cost micros per
  turn operation ($0.003640);
- an 18-turn run with no repairs projects 32,760 cost micros ($0.032760);
- an 18-turn run where every decision repairs projects 65,520 cost micros
  ($0.065520), below the $0.25 run cap.

SQLite reserves the maximum two-agent/two-attempt operation amount before any
dispatch. Reservation checks include operation, run, guest UTC day, deployment
UTC day, the 40-attempt run ceiling, and provider concurrency caps. Settlement uses reported usage when
present; timeout or missing-usage paths charge a conservative bounded estimate.
Retry usage is included. Negative or above-reservation settlement is rejected.

Default configuration remains `Provider:Mode=scripted` and requires no secret.
An internal live deployment sets `Provider__Mode=live` and server-only
`Provider__ApiKey`; the key is read only while constructing the adapter and is
never serialized into a context, checkpoint, response, or browser contract.
The model and three operating cost caps can be overridden through the matching
`Provider__...` configuration keys without changing game rules.
