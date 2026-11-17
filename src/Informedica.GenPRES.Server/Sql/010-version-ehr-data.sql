-- Migration 10: the EHR data read when a version was signed, as JSON with its structure
-- version; null for a version signed without EHR data. From JSON structure version 14 the
-- column plan holds the order plan alone. The loader checks that both columns are present or
-- both are null; a column added later cannot carry that check.
alter table order_plan add column ehr_json_version integer null;
alter table order_plan add column ehr_data text null;
