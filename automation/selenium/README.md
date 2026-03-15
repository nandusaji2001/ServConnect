# Selenium User Automation Suite

This suite validates user-level flows for:
- House rentals
- Local events
- TransitConnect
- Community
- News

## Test Scope

The test file `test_user_journeys.py` covers login plus one flow for each requested module.

## Prerequisites

- Python 3.10+
- Microsoft Edge or Google Chrome installed
- .NET SDK (to run backend)

## Install Python dependencies

```powershell
cd automation/selenium
python -m pip install -r requirements.txt
```

## Run end-to-end tests and generate HTML report

```powershell
cd automation/selenium
.\run_selenium_tests.ps1
```

## Open the generated report

The report is generated at:

- `automation/selenium/reports/selenium-report.html`

Open that file in any browser to review pass/fail status, duration, and failure screenshots.

Each test entry also includes a detailed step log table (actions, entered values, and navigation flow).

## Optional overrides

You can override runtime values with environment variables or script parameters:

- `BASE_URL` (default: `https://localhost:7213`)
- `BROWSER` (`chrome` or `edge`, default: `chrome`)
- `E2E_USER_EMAIL` (default: `user@example.com`)
- `E2E_USER_PASSWORD` (default: `Test@123`)
- `HEADLESS` (`true` or `false`)
