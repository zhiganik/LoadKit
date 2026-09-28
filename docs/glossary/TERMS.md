# Terms

> Status: ready

| Term | Meaning |
|---|---|
| Scenario | A JSON file describing the load: target, auth, requests, parameters, thresholds |
| Concurrency | Number of requests "in flight" at the same time |
| Closed model | A new request is sent only after the response to the previous one in the same worker |
| Open model | Requests are sent at a given rate regardless of responses (v2) |
| Warmup | First requests that are sent but not counted in metrics |
| Percentile pN | The time within which N% of requests completed; nearest-rank |
| Preflight | Preparation before a run: acquiring a token and checking reachability |
| Threshold | A limit for a metric; violation → exit code 1 |
| `loadrun` | Run tag in the query string for filtering in Application Insights |
| Hot path | The request sending loop; any extra overhead distorts metrics |
| Skill | A folder with `SKILL.md` following the Agent Skills standard; instructions for an AI agent |
| TargetApi | Sample API from `samples/` for tests and demos |
