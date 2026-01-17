import {
  Button,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Typography,
} from "@mui/material";
import { DateTimePicker } from "@mui/x-date-pickers";
import dayjs, { Dayjs } from "dayjs";
import { useEffect, useMemo, useState } from "react";

type Props = {
  open: boolean;
  scheduledStart: string;
  scheduledEnd?: string;
  defaultCheckInTime?: string | null;
  defaultCheckOutTime?: string | null;
  onClose: () => void;
  onConfirm: (
    selectedIso: string,
    info: {
      isEarly: boolean;
      isLate: boolean;
      days: number;
      hours: number;
      minutes: number;
    },
  ) => void;
};

export default function CheckInTimeDialog({
  open,
  scheduledStart,
  scheduledEnd,
  defaultCheckInTime,
  defaultCheckOutTime,
  onClose,
  onConfirm,
}: Props) {
  const [value, setValue] = useState<Dayjs>(dayjs());

  const scheduled = useMemo(() => dayjs(scheduledStart), [scheduledStart]);

  const displayScheduledStart = useMemo(() => {
    const base = dayjs(scheduledStart);
    const def = defaultCheckInTime ? dayjs(defaultCheckInTime) : null;
    if (def && base.isValid() && def.isValid()) {
      return base
        .hour(def.hour())
        .minute(def.minute())
        .second(0)
        .millisecond(0);
    }
    return base;
  }, [scheduledStart, defaultCheckInTime]);

  const displayScheduledEnd = useMemo(() => {
    if (!scheduledEnd) return null as Dayjs | null;
    const base = dayjs(scheduledEnd);
    const def = defaultCheckOutTime ? dayjs(defaultCheckOutTime) : null;
    if (def && base.isValid() && def.isValid()) {
      return base
        .hour(def.hour())
        .minute(def.minute())
        .second(0)
        .millisecond(0);
    }
    return base;
  }, [scheduledEnd, defaultCheckOutTime]);

  const { isEarly, isLate, days, hours, minutes } = useMemo(() => {
    const base = displayScheduledStart || scheduled;
    const early = value.isBefore(base);
    const late = value.isAfter(base);
    const diff = Math.abs(value.diff(base, "minute"));
    const d = Math.floor(diff / 1440);
    const h = Math.floor((diff % 1440) / 60);
    const m = diff % 60;
    return { isEarly: early, isLate: late, days: d, hours: h, minutes: m };
  }, [value, scheduled, displayScheduledStart]);

  useEffect(() => {
    if (open) setValue(dayjs());
  }, [open, displayScheduledStart]);

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Thời gian Check-in</DialogTitle>
      <DialogContent>
        <Stack spacing={1.5} sx={{ mt: 1 }}>
          <Stack direction="row" spacing={1} alignItems="center">
            <Typography variant="subtitle2" fontWeight={700}>
              Dự kiến nhận phòng:
            </Typography>
            <Chip label={displayScheduledStart.format("DD/MM/YYYY HH:mm")} />
          </Stack>
          {scheduledEnd ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <Typography variant="subtitle2" fontWeight={700}>
                Dự kiến trả phòng:
              </Typography>
              <Chip
                label={(displayScheduledEnd || dayjs(scheduledEnd)).format(
                  "DD/MM/YYYY HH:mm",
                )}
              />
            </Stack>
          ) : null}
          <DateTimePicker
            label="Thời gian check-in"
            value={value}
            minDateTime={displayScheduledStart}
            maxDateTime={(displayScheduledStart || dayjs(scheduledStart))
              .hour(23)
              .minute(59)
              .second(0)
              .millisecond(0)}
            slotProps={{
              textField: {
                readOnly: true,
                inputProps: {
                  readOnly: true,
                },
                fullWidth: true,
              },
            }}
            onChange={(v) => v && setValue(v)}
          />
          <Stack direction="row" spacing={1} alignItems="center">
            <Chip
              color={isEarly || isLate ? "warning" : "success"}
              label={
                isEarly
                  ? `Sớm ${days}d ${hours}h ${minutes}m`
                  : isLate
                    ? `Muộn ${days}d ${hours}h ${minutes}m`
                    : `Đúng giờ`
              }
            />
          </Stack>
          {isEarly ? (
            <Typography variant="body2" color="error" fontWeight={700}>
              * Cần check in đúng giờ
            </Typography>
          ) : null}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Hủy</Button>
        <Button
          variant="contained"
          // disabled={isEarly}
          onClick={() =>
            onConfirm(value.format("YYYY-MM-DDTHH:mm:ss"), {
              isEarly,
              isLate,
              days,
              hours,
              minutes,
            })
          }
        >
          Xác nhận
        </Button>
      </DialogActions>
    </Dialog>
  );
}
