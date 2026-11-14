# Codecov API v2 fixtures

Answers of `GET /api/v2/{service}/{owner}/repos/{repo}/commits/`, written by hand from the public
documentation of the Codecov API v2 (the commits list and its `totals` object). They were **never
compared with a real answer**: their fidelity is unproven until the first real import (owner step of
M4.08). SHAs, owners and branches are synthetic.

- `commits-page-1.json`: two complete commits, one commit still processing, one without totals, one
  with an invalid SHA, and a `next` link.
- `commits-page-2.json`: the last page (`next` is `null`).
