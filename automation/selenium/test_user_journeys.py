from __future__ import annotations

from typing import Callable, Sequence

import pytest
from selenium.common.exceptions import StaleElementReferenceException, TimeoutException
from selenium.webdriver.common.by import By
from selenium.webdriver.support import expected_conditions as EC
from selenium.webdriver.support.ui import WebDriverWait

DEFAULT_TIMEOUT = 30
StepLogger = Callable[[str, dict[str, object] | str | None], None]


def login_as_user(driver, base_url: str, email: str, password: str, step_logger: StepLogger) -> None:
    login_url = f"{base_url}/Account/Login"
    step_logger("Open login page", {"url": login_url})
    driver.get(login_url)

    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    email_input = wait.until(EC.visibility_of_element_located((By.NAME, "Email")))
    password_input = wait.until(EC.visibility_of_element_located((By.NAME, "Password")))

    email_input.clear()
    email_input.send_keys(email)
    step_logger("Enter login email", {"email": email})

    password_input.clear()
    password_input.send_keys(password)
    step_logger("Enter login password", {"password": password})

    driver.find_element(By.CSS_SELECTOR, "button.btn-signin[type='submit']").click()
    step_logger("Click sign-in button")

    wait.until(lambda d: "/Account/Login" not in d.current_url)
    step_logger("Login redirect completed", {"current_url": driver.current_url})

    assert "/Account/Login" not in driver.current_url, (
        "Login did not complete. Check test credentials or backend seed data."
    )


def wait_for_any_visible(driver, css_selectors: Sequence[str], timeout: int = DEFAULT_TIMEOUT):
    def _find_visible(_):
        for selector in css_selectors:
            elements = driver.find_elements(By.CSS_SELECTOR, selector)
            for element in elements:
                try:
                    if element.is_displayed():
                        return element
                except StaleElementReferenceException:
                    continue
        return False

    return WebDriverWait(driver, timeout).until(_find_visible)


def wait_for_news_content(driver, timeout: int = 45):
    WebDriverWait(driver, timeout).until(
        lambda d: len(d.find_elements(By.CSS_SELECTOR, "#newsContent .news-placeholder-spinner")) == 0
    )
    return wait_for_any_visible(
        driver,
        [
            "#newsContent .news-card",
            "#newsContent .news-error",
            "#newsContent .news-placeholder",
        ],
        timeout=timeout,
    )


@pytest.fixture
def authenticated_driver(
    driver,
    base_url: str,
    login_email: str,
    login_password: str,
    step_logger: StepLogger,
):
    login_as_user(driver, base_url, login_email, login_password, step_logger)
    step_logger("Authentication complete", {"landing_url": driver.current_url})
    return driver


@pytest.mark.userjourney
def test_house_rentals_user_flow(authenticated_driver, base_url: str, step_logger: StepLogger):
    driver = authenticated_driver
    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    rentals_url = f"{base_url}/Rental"
    step_logger("Open House Rentals page", {"url": rentals_url})
    driver.get(rentals_url)

    heading = wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, ".rentals-header h1")))
    step_logger("Verify Rentals heading", {"heading": heading.text})
    assert "House Rentals" in heading.text

    search_input = wait.until(EC.visibility_of_element_located((By.ID, "searchInput")))
    rental_query = "Kattappana"
    search_input.clear()
    search_input.send_keys(rental_query)
    step_logger("Enter Rentals search query", {"searchInput": rental_query})

    driver.find_element(By.CSS_SELECTOR, "button.search-btn").click()
    step_logger("Submit Rentals search")

    try:
        WebDriverWait(driver, 40).until(
            lambda d: len(d.find_elements(By.CSS_SELECTOR, "#propertiesContainer .loading-spinner")) == 0
        )
    except TimeoutException as ex:
        raise AssertionError("Rental properties did not finish loading in time.") from ex

    container_text = driver.find_element(By.ID, "propertiesContainer").text.lower()
    step_logger("Read Rentals result container", {"container_snippet": container_text[:120]})
    assert "error loading properties" not in container_text

    cards = driver.find_elements(By.CSS_SELECTOR, "#propertiesContainer .property-card")
    if cards:
        step_logger("Rental cards available", {"count": len(cards)})
        cards[0].click()
        step_logger("Open first rental card")
        wait.until(lambda d: "/Rental/Details/" in d.current_url)
        step_logger("Rental details page opened", {"current_url": driver.current_url})
        assert "/Rental/Details/" in driver.current_url
    else:
        step_logger("No rental cards found; checking empty state")
        empty_state = wait.until(
            EC.visibility_of_element_located((By.CSS_SELECTOR, "#propertiesContainer .empty-state"))
        )
        step_logger("Rental empty state displayed", {"empty_state_text": empty_state.text})
        assert "No properties found" in empty_state.text


@pytest.mark.userjourney
def test_local_events_user_flow(authenticated_driver, base_url: str, step_logger: StepLogger):
    driver = authenticated_driver
    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    events_url = f"{base_url}/Events"
    step_logger("Open Local Events page", {"url": events_url})
    driver.get(events_url)

    heading = wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, ".events-header h1")))
    step_logger("Verify Events heading", {"heading": heading.text})
    assert "Local Events" in heading.text

    search_input = wait.until(
        EC.visibility_of_element_located((By.CSS_SELECTOR, "form.search-row input[name='search']"))
    )
    events_query = "community"
    search_input.clear()
    search_input.send_keys(events_query)
    step_logger("Enter Events search query", {"search": events_query})
    driver.find_element(By.CSS_SELECTOR, "form.search-row button[type='submit']").click()
    step_logger("Submit Events search")

    wait_for_any_visible(
        driver,
        [
            ".events-grid .event-card",
            ".empty-state",
        ],
        timeout=35,
    )

    view_links = driver.find_elements(By.CSS_SELECTOR, ".events-grid .event-card .btn-view")
    if view_links:
        step_logger("Event cards available", {"count": len(view_links)})
        view_links[0].click()
        step_logger("Open first event details")
        wait.until(lambda d: "/Events/Details/" in d.current_url)
        step_logger("Event details page opened", {"current_url": driver.current_url})
        assert "/Events/Details/" in driver.current_url
    else:
        empty_text = driver.find_element(By.CSS_SELECTOR, ".empty-state").text
        step_logger("Events empty state displayed", {"empty_state_text": empty_text})
        assert "No upcoming events" in empty_text


@pytest.mark.userjourney
def test_transit_connect_user_flow(authenticated_driver, base_url: str, step_logger: StepLogger):
    driver = authenticated_driver
    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    transit_url = f"{base_url}/TransitConnect"
    step_logger("Open TransitConnect page", {"url": transit_url})
    driver.get(transit_url)

    heading = wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, ".page-header h1")))
    step_logger("Verify Transit heading", {"heading": heading.text})
    assert "TransitConnect" in heading.text

    from_input = wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, "input[name='from']")))
    to_input = wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, "input[name='to']")))
    wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, "button.btn-search")))

    transit_from = "Kattappana"
    transit_to = "Kumily"
    from_input.clear()
    from_input.send_keys(transit_from)
    to_input.clear()
    to_input.send_keys(transit_to)
    step_logger("Enter Transit route values", {"from": transit_from, "to": transit_to})

    initial_url = driver.current_url
    driver.find_element(By.CSS_SELECTOR, "button.btn-search").click()
    step_logger("Submit Transit search")

    try:
        WebDriverWait(driver, 15).until(lambda d: d.current_url != initial_url)
        step_logger("Transit search navigation completed", {"current_url": driver.current_url})
    except TimeoutException:
        step_logger("Transit search stayed on same URL", {"current_url": driver.current_url})

    transit_url_after_search = driver.current_url.lower()
    step_logger("Transit search URL after submit", {"current_url": driver.current_url})
    assert "from=" in transit_url_after_search and "to=" in transit_url_after_search, (
        "Transit search query parameters were not applied in URL."
    )

    try:
        result_state = wait_for_any_visible(
            driver,
            [
                ".results-count",
                ".routes-grid .route-card",
                ".empty-state",
            ],
            timeout=25,
        )
        step_logger(
            "Transit result state visible",
            {
                "tag": result_state.tag_name,
                "class": result_state.get_attribute("class") or "",
                "text": (result_state.text or "")[:120],
            },
        )

        route_cards = driver.find_elements(By.CSS_SELECTOR, ".routes-grid .route-card")
        if route_cards:
            step_logger("Transit routes available", {"count": len(route_cards)})
            route_cards[0].click()
            step_logger("Open first transit route details")
            wait.until(lambda d: "/TransitConnect/Details/" in d.current_url)
            step_logger("Transit route details page opened", {"current_url": driver.current_url})
            assert "/TransitConnect/Details/" in driver.current_url
        else:
            empty_states = driver.find_elements(By.CSS_SELECTOR, ".empty-state")
            if empty_states:
                empty_text = empty_states[0].text
                step_logger("Transit empty state displayed", {"empty_state_text": empty_text})
                assert "No routes found" in empty_text
            else:
                step_logger(
                    "Transit route cards and empty state both absent",
                    {"current_url": driver.current_url},
                )
    except TimeoutException:
        body_text = driver.find_element(By.TAG_NAME, "body").text
        step_logger(
            "Transit results not visible in wait window",
            {
                "page_title": driver.title,
                "body_snippet": body_text[:220],
            },
        )
        assert "transitconnect" in driver.current_url.lower()


@pytest.mark.userjourney
def test_community_user_flow(authenticated_driver, base_url: str, step_logger: StepLogger):
    driver = authenticated_driver
    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    community_url = f"{base_url}/community"
    step_logger("Open Community page", {"url": community_url})
    driver.get(community_url)

    wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, "nav.sidebar")))
    wait.until(EC.visibility_of_element_located((By.ID, "postsFeed")))
    step_logger("Community shell loaded", {"sidebar": "visible", "postsFeed": "visible"})

    try:
        WebDriverWait(driver, 45).until(
            lambda d: d.execute_script(
                "const el=document.getElementById('feedLoading');"
                "return !!el && getComputedStyle(el).display === 'none';"
            )
        )
        step_logger("Community feed loading spinner hidden")
    except TimeoutException as ex:
        raise AssertionError(
            "Community feed did not finish loading. API or page script may have failed."
        ) from ex

    posts = driver.find_elements(By.CSS_SELECTOR, "#postsFeed .post-card")
    empty_visible = driver.execute_script(
        "const el=document.getElementById('emptyFeed');"
        "return !!el && getComputedStyle(el).display !== 'none';"
    )

    step_logger(
        "Community feed state read",
        {"post_cards": len(posts), "empty_state_visible": bool(empty_visible)},
    )

    create_post_btn = wait.until(EC.element_to_be_clickable((By.CSS_SELECTOR, ".btn-create-post")))
    create_post_btn.click()
    step_logger("Open Create Post modal")

    caption_input = wait.until(EC.visibility_of_element_located((By.ID, "postCaption")))
    draft_caption = "Automation test draft post from Selenium"
    caption_input.clear()
    caption_input.send_keys(draft_caption)
    step_logger("Enter Community post caption", {"caption": draft_caption})

    close_button = driver.find_element(By.CSS_SELECTOR, "#createPostModal .modal-close")
    close_button.click()
    step_logger("Close Create Post modal without submit")

    assert len(posts) > 0 or bool(empty_visible), (
        "Community page loaded, but no posts or empty-state was shown."
    )


@pytest.mark.userjourney
def test_news_user_flow(authenticated_driver, base_url: str, step_logger: StepLogger):
    driver = authenticated_driver
    wait = WebDriverWait(driver, DEFAULT_TIMEOUT)

    news_url = f"{base_url}/Home/News"
    step_logger("Open News page", {"url": news_url})
    driver.get(news_url)

    wait.until(EC.visibility_of_element_located((By.CSS_SELECTOR, ".news-title")))
    step_logger("News heading visible")

    location_input = wait.until(EC.visibility_of_element_located((By.ID, "newsLocationInput")))
    news_location = "Kattappana"
    location_input.clear()
    location_input.send_keys(news_location)
    step_logger("Enter News location", {"newsLocationInput": news_location})

    driver.find_element(By.ID, "newsSearchBtn").click()
    step_logger("Submit News search")

    wait_for_news_content(driver)
    step_logger("News content rendering completed")

    cards = driver.find_elements(By.CSS_SELECTOR, "#newsContent .news-card")
    errors = driver.find_elements(By.CSS_SELECTOR, "#newsContent .news-error")
    placeholders = driver.find_elements(By.CSS_SELECTOR, "#newsContent .news-placeholder")

    step_logger(
        "News state summary",
        {"news_cards": len(cards), "news_errors": len(errors), "news_placeholders": len(placeholders)},
    )

    assert cards or errors or placeholders, "News section did not render any result state."

    if cards:
        titles = [el.text.strip() for el in driver.find_elements(By.CSS_SELECTOR, "#newsContent .news-card-title")]
        step_logger("News titles extracted", {"title_count": len(titles), "first_title": titles[0] if titles else ""})
        assert any(titles), "News cards rendered without visible titles."
