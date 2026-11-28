import pytest

from k12_pipeline.api_client import IMPORT_KEY_ENV, IMPORT_KEY_HEADER, api_client


@pytest.mark.parametrize(
    ("env_value", "expected"),
    [(None, None), ("", None), ("   ", None), ("abc123", "abc123"), (" abc123 ", "abc123")],
)
def test_sends_the_import_key_only_when_it_is_set(monkeypatch, env_value, expected):
    if env_value is None:
        monkeypatch.delenv(IMPORT_KEY_ENV, raising=False)
    else:
        monkeypatch.setenv(IMPORT_KEY_ENV, env_value)
    with api_client("https://example.org") as client:
        assert client.headers.get(IMPORT_KEY_HEADER) == expected
        assert str(client.base_url) == "https://example.org"
