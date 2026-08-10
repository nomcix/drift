# Migration 0003 — P9 mastery loop

Migration `20260810014342_P9Mastery` advances schema metadata to version `3`.
It expands the previously unused P4 certification stubs into durable locked
certificates and records run kind, assistance, certification ownership, slot,
and server-held variant disclosure.

Existing runs migrate as unassisted practice runs. P8 exposed no route that
could create a certification row, so the new required certificate metadata
does not rewrite a supported persisted certificate. Build versions and runs
remain immutable and no authored contract version changes.

Historical completed certificates retain their exact build version, provider
profile, mission content, rules, score, and certification-pool versions. A
later content or provider-profile change therefore neither relabels nor
invalidates an existing certificate.
