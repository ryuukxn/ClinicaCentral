-- Run once against the clinicacentral database before using multiple prescriptions
-- per medical record or the Emergency time-off type.
ALTER TABLE prescriptions
    DROP PRIMARY KEY,
    ADD COLUMN prescription_id BIGINT NOT NULL AUTO_INCREMENT FIRST,
    ADD PRIMARY KEY (prescription_id),
    ADD INDEX ix_prescriptions_medical_record_id (id);

ALTER TABLE time_off
    MODIFY COLUMN type VARCHAR(50) NOT NULL;

ALTER TABLE time_off
    MODIFY COLUMN reason TEXT NULL;

ALTER TABLE medical_records
    ADD COLUMN appointment_id INT NULL,
    ADD UNIQUE INDEX ix_medical_records_appointment_id (appointment_id);
