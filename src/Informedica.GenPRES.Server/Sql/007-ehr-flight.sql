-- Migration 7: the EHR data a data notice was told over and a challenge was issued over, as
-- read, beside the projection each keeps for the client and the version: the notice is
-- accepted over exactly the read it was told over, and the next challenge compares against the
-- read just signed on. Stored as the Dto under its own structure version, as the opened-with
-- row holds it. Nullable and ignored by the release before; null where the read was none.
alter table data_notice add column ehr_json_version integer null;
alter table data_notice add column ehr_data text null;
alter table challenge add column ehr_json_version integer null;
alter table challenge add column ehr_data text null;
