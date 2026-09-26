-- Migration 8: what the user measured in a Session. A weight, a height or a gestational age
-- measured at the bedside stays for the Session, whatever the EHR reads later, and a resume
-- restores it: each in its own row, dated at the request that carried it, written only when
-- the value differs from the one the Session stood on. The latest row per kind decides; a row
-- with no value clears the measurement, so that a cleared weight does not return at a resume.
create table measurement (
    id         integer primary key,
    session_id text not null references session (session_id),
    kind       text not null check (kind in ('weight', 'height', 'gestage')),
    value      integer null,        -- grams, centimeters or weeks; null: cleared
    days       integer null,        -- the days beside the weeks of a gestational age
    at         integer not null,
    check (days is null or kind = 'gestage')
);
create index ix_measurement on measurement (session_id, id);
