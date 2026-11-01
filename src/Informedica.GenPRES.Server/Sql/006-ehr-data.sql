-- Migration 6: the EHR data a Session opened on, as read: the core patient with the identity
-- on it plus the data only the rules read, stored as its Dto under its own structure version;
-- null for a Session that opened on no EHR data.
alter table session_opened_with add column ehr_json_version integer null;
alter table session_opened_with add column ehr_data text null;
