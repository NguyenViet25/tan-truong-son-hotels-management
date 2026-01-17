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
  scheduledEnd: string;
  scheduledStart?: string;
  extendedDate?: string | null;
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
  reload?: number;
};

export default function CheckOutTimeDialog({
  open,
  extendedDate,
  scheduledEnd,
  scheduledStart,
  defaultCheckInTime,
  defaultCheckOutTime,
  onClose,
  onConfirm,
  reload,
}: Props) {
  const [value, setValue] = useState<Dayjs>(dayjs());

  const scheduled = useMemo(() => dayjs(scheduledEnd), [scheduledEnd]);

  const displayScheduledStart = useMemo(() => {
    if (!scheduledStart) return null as Dayjs | null;
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
    const endDate = extendedDate ?? scheduledEnd;
    const base = dayjs(endDate);
    const def = defaultCheckOutTime ? dayjs(defaultCheckOutTime) : null;
    if (def && base.isValid() && def.isValid()) {
      return base
        .hour(def.hour())
        .minute(def.minute())
        .second(0)
        .millisecond(0);
    }
    return base;
  }, [scheduledEnd, defaultCheckOutTime, reload]);
  const defaultCheckoutByCurrent = useMemo(() => {
    if (!defaultCheckOutTime) return null as Dayjs | null;
    const def = dayjs(defaultCheckOutTime);
    if (!def.isValid()) return null as Dayjs | null;
    const now = dayjs();
    const todayAt = now
      .hour(def.hour())
      .minute(def.minute())
      .second(0)
      .millisecond(0);
    const target = now.isAfter(todayAt) ? todayAt.add(1, "day") : todayAt;
    return target;
  }, [defaultCheckOutTime]);

  useEffect(() => {
    if (open) setValue(dayjs());
  }, [open, defaultCheckoutByCurrent]);

  const { isEarly, isLate, days, hours, minutes } = useMemo(() => {
    const base = displayScheduledEnd || scheduled;
    const early = value.isBefore(base);
    const late = value.isAfter(base);
    const diff = Math.abs(value.diff(base, "minute"));
    const d = Math.floor(diff / 1440);
    const h = Math.floor((diff % 1440) / 60);
    const m = diff % 60;
    return { isEarly: early, isLate: late, days: d, hours: h, minutes: m };
  }, [value, scheduled, displayScheduledEnd]);

  // No auto-confirm; user must click 'Xác nhận'

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Thời gian Check-out</DialogTitle>
      <DialogContent>
        <Stack spacing={1.5} sx={{ mt: 1 }}>
          {scheduledStart ? (
            <Stack direction="row" spacing={1} alignItems="center">
              <Typography variant="subtitle2" fontWeight={700}>
                Dự kiến nhận phòng:
              </Typography>
              <Chip
                label={(displayScheduledStart || dayjs(scheduledStart)).format(
                  "DD/MM/YYYY HH:mm",
                )}
              />
            </Stack>
          ) : null}
          <Stack direction="row" spacing={1} alignItems="center">
            <Typography variant="subtitle2" fontWeight={700}>
              Dự kiến trả phòng:
            </Typography>
            <Chip label={displayScheduledEnd.format("DD/MM/YYYY HH:mm")} />
          </Stack>
          <DateTimePicker
            label="Thời gian check-out"
            value={value}
            disabled
            minDateTime={dayjs()}
            maxDateTime={
              dayjs().isAfter(dayjs(displayScheduledEnd).add(1, "minute"))
                ? dayjs(displayScheduledEnd).add(1, "minute")
                : dayjs()
            }
            onChange={(v) => v && setValue(v)}
          />
          <Stack direction="row" spacing={1} alignItems="center">
            <Chip
              color={isLate || isEarly ? "warning" : "success"}
              label={
                isLate
                  ? `Muộn ${days}d ${hours}h ${minutes}m`
                  : isEarly
                    ? `Sớm ${days}d ${hours}h ${minutes}m`
                    : `Đúng giờ`
              }
            />
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Hủy</Button>
        <Button
          variant="contained"
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
