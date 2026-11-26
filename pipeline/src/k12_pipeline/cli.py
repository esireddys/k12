"""Command line entry point. Each subcommand becomes an Azure Container Apps job."""

import argparse

from .sources import RELEASED_TESTS


def main() -> None:
    parser = argparse.ArgumentParser(prog="k12-pipeline")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("sources", help="List the released tests the importer knows about")
    sub.add_parser("import", help="Download and extract released tests (not built yet)")
    sub.add_parser("review", help="Run the AI reviewer over pending drafts (not built yet)")
    args = parser.parse_args()

    if args.command == "sources":
        for t in RELEASED_TESTS:
            print(f"{t.jurisdiction} {t.exam} {t.level} {t.year} {t.season} tagged={t.tagged} {t.url}")
    else:
        raise SystemExit(f"'{args.command}' is not built yet.")


if __name__ == "__main__":
    main()
