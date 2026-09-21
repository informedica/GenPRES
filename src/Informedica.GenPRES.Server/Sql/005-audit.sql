-- Migration 5 (plan 516 step 9): what was done, by whom and to which Session, written in the
-- same transaction as the act it describes, so that an entry cannot exist for an act that did
-- not land, nor an act land without its entry.
--
-- Append-only and never dropped: unlike the launches, the notices and the challenges, an audit
-- entry outlives what it describes. Its retention and the legal basis for keeping the mail
-- address a launch was refused for are open questions of the plan (#516), to be answered
-- before a production engine holds this table.
create table audit_entry (
    id         integer primary key,
    at         integer not null,        -- unix ms
    session_id text null,               -- null before a Session exists, as at a launch
    actor      text null,               -- the user id; null when the act had no known User
    action     text not null,           -- launched | opened | refused | signed | pin-set | ...
    outcome    text not null,           -- ok | refused
    detail     text null                -- what the action needs beside its name, as json
);
create index ix_audit_session on audit_entry (session_id, id);
create index ix_audit_at on audit_entry (at);
