# Preserve forensic timestamps in a CSV-shaped JSON Lines export

The JSON export exposes the same seven fields and records as CSV, with native numeric and boolean values, explicit null timestamps, and UTC ISO 8601 timestamps retaining seven fractional-second digits. Unlike CSV, JSON dates do not use `--dt`: consumers need a stable ingestion format, while existing CSV workflows retain their custom formatting. Each record occupies one line in a `.json` file, following MFTECmd; Elasticsearch mapping determines indexed date precision separately from the precision preserved in the export.
