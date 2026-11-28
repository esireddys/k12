"""HTTP client for the K12 API, local or deployed."""

from __future__ import annotations

import os

import httpx

IMPORT_KEY_ENV = "K12_IMPORT_KEY"
IMPORT_KEY_HEADER = "X-Import-Key"


def api_client(base_url: str, timeout: float = 180) -> httpx.Client:
    """A client for the API. A local API in Development needs no sign-in. A deployed one needs the import key,
    read from the K12_IMPORT_KEY environment variable (never a command-line argument, so it stays out of shell history).
    The long timeout covers an Azure SQL database that is waking up from a pause."""
    headers = {}
    if key := os.environ.get(IMPORT_KEY_ENV, "").strip():
        headers[IMPORT_KEY_HEADER] = key
    return httpx.Client(base_url=base_url, timeout=timeout, follow_redirects=True, headers=headers)
