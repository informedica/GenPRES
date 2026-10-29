-- Migration 10: the mark as an adult, held as a measurement row beside the weight, the height and
-- the gestational age, so that a resume restores it. SQLite cannot change a check, so the table is
-- made anew with the kind 'marked-adult' allowed, its value 1 for marked and 0 for cleared and no days,
-- and the rows are copied over.
create table measurement_new (
    id         integer primary key,
    session_id text not null references session (session_id),
    kind       text not null check (kind in ('weight', 'height', 'gestage', 'marked-adult')),
    value      integer null,        -- grams, centimeters, weeks, or 1 and 0 for the mark; null: cleared
    days       integer null,        -- the days beside the weeks of a gestational age
    at         integer not null,
    -- a gestational age has its weeks and its days, or neither; the mark is 1 or 0; the other
    -- kinds have no days
    check (
        case kind
            when 'gestage' then (value is null) = (days is null)
            when 'marked-adult' then value in (0, 1) and days is null
            else days is null
        end
    )
);
insert into measurement_new (id, session_id, kind, value, days, at)
    select id, session_id, kind, value, days, at from measurement;
drop table measurement;
alter table measurement_new rename to measurement;
create index ix_measurement on measurement (session_id, id);
