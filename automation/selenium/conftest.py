from __future__ import annotations

import base64
import html
import os
from datetime import datetime
from pathlib import Path
from typing import Any

import pytest
from selenium import webdriver
from selenium.webdriver.chrome.options import Options as ChromeOptions
from selenium.webdriver.edge.options import Options as EdgeOptions

REPORTS_DIR = Path(__file__).resolve().parent / "reports"


def _as_bool(value: str | None, default: bool) -> bool:
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "y", "on"}


def _build_edge_driver(headless: bool) -> webdriver.Edge:
    options = EdgeOptions()
    options.set_capability("acceptInsecureCerts", True)
    if headless:
        options.add_argument("--headless=new")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--disable-gpu")
    options.add_argument("--ignore-certificate-errors")
    options.add_argument("--disable-dev-shm-usage")
    return webdriver.Edge(options=options)


def _build_chrome_driver(headless: bool) -> webdriver.Chrome:
    options = ChromeOptions()
    options.set_capability("acceptInsecureCerts", True)
    if headless:
        options.add_argument("--headless=new")
    options.add_argument("--window-size=1920,1080")
    options.add_argument("--disable-gpu")
    options.add_argument("--ignore-certificate-errors")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--no-sandbox")
    return webdriver.Chrome(options=options)


@pytest.fixture(scope="session")
def base_url() -> str:
    return os.getenv("BASE_URL", "https://localhost:7213").rstrip("/")


@pytest.fixture(scope="session")
def login_email() -> str:
    return os.getenv("E2E_USER_EMAIL", "user@example.com")


@pytest.fixture(scope="session")
def login_password() -> str:
    return os.getenv("E2E_USER_PASSWORD", "Test@123")


@pytest.fixture(scope="session")
def browser_name() -> str:
    return os.getenv("BROWSER", "chrome").strip().lower()


@pytest.fixture(scope="session")
def headless() -> bool:
    return _as_bool(os.getenv("HEADLESS"), default=True)


@pytest.fixture
def driver(browser_name: str, headless: bool):
    if browser_name == "edge":
        web_driver = _build_edge_driver(headless)
    elif browser_name == "chrome":
        web_driver = _build_chrome_driver(headless)
    else:
        raise ValueError("Unsupported BROWSER value. Use 'edge' or 'chrome'.")

    REPORTS_DIR.mkdir(parents=True, exist_ok=True)
    web_driver.set_page_load_timeout(60)

    yield web_driver

    web_driver.quit()


def _render_step_log_table(step_logs: list[dict[str, str]]) -> str:
    if not step_logs:
        return "<div>No step logs captured.</div>"

    rows = []
    for index, entry in enumerate(step_logs, start=1):
        rows.append(
            "<tr>"
            f"<td style='padding:6px 10px;border:1px solid #ddd;'>{index}</td>"
            f"<td style='padding:6px 10px;border:1px solid #ddd;'>{html.escape(entry.get('time', ''))}</td>"
            f"<td style='padding:6px 10px;border:1px solid #ddd;'>{html.escape(entry.get('action', ''))}</td>"
            f"<td style='padding:6px 10px;border:1px solid #ddd;'>{html.escape(entry.get('details', ''))}</td>"
            "</tr>"
        )

    return (
        "<details open>"
        f"<summary><strong>Detailed Step Log ({len(step_logs)} steps)</strong></summary>"
        "<table style='border-collapse:collapse;margin-top:8px;font-size:13px;'>"
        "<thead><tr>"
        "<th style='padding:6px 10px;border:1px solid #ddd;background:#f6f8fa;'>#</th>"
        "<th style='padding:6px 10px;border:1px solid #ddd;background:#f6f8fa;'>Time</th>"
        "<th style='padding:6px 10px;border:1px solid #ddd;background:#f6f8fa;'>Action</th>"
        "<th style='padding:6px 10px;border:1px solid #ddd;background:#f6f8fa;'>Details</th>"
        "</tr></thead>"
        f"<tbody>{''.join(rows)}</tbody></table></details>"
    )


@pytest.fixture
def step_logger(request):
    request.node._step_logs = []

    def _log(action: str, details: dict[str, Any] | str | None = None) -> None:
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        if isinstance(details, dict):
            details_text = ", ".join(f"{key}={value}" for key, value in details.items())
        elif details is None:
            details_text = ""
        else:
            details_text = str(details)

        request.node._step_logs.append(
            {
                "time": timestamp,
                "action": action,
                "details": details_text,
            }
        )

    return _log


@pytest.hookimpl(hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    report = outcome.get_result()

    if report.when != "call":
        return

    try:
        from pytest_html import extras

        report.extras = getattr(report, "extras", [])

        step_logs = getattr(item, "_step_logs", [])
        report.extras.append(extras.html(_render_step_log_table(step_logs)))

        if report.failed:
            web_driver = item.funcargs.get("driver")
            if web_driver is not None:
                png = web_driver.get_screenshot_as_png()
                png_base64 = base64.b64encode(png).decode("utf-8")
                report.extras.append(extras.png(png_base64, name="failure-screenshot"))
    except Exception:
        # Keep report generation resilient even if screenshot capture fails.
        pass
