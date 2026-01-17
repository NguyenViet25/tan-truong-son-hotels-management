import {
  Box,
  Snackbar,
  Alert,
  Button,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Stack,
  Typography,
  IconButton,
  TextField,
} from "@mui/material";
import React, { useEffect, useMemo, useState } from "react";
import { DatePicker } from "@mui/x-date-pickers";
import { Dayjs } from "dayjs";
import guestsApi, {
  type CreateGuestRequest,
  type GuestDto,
  type UpdateGuestRequest,
} from "../../../../api/guestsApi";
import DataTable, {
  type Column,
} from "../../../../components/common/DataTable";
import PageTitle from "../../../../components/common/PageTitle";
import GuestFormModal from "./components/GuestFormModal";
import { Add, History } from "@mui/icons-material";
import { useNavigate } from "react-router-dom";

const GuestsManagementPage: React.FC = () => {
  const [items, setItems] = useState<GuestDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [total, setTotal] = useState(0);
  const [searchText, setSearchText] = useState("");
  const [fromDate, setFromDate] = useState<Dayjs | null>(null);
  const [toDate, setToDate] = useState<Dayjs | null>(null);

  const [editOpen, setEditOpen] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<GuestDto | null>(null);
  const [viewOpen, setViewOpen] = useState(false);
  const [viewItem, setViewItem] = useState<GuestDto | null>(null);

  const [snackbar, setSnackbar] = useState<{
    open: boolean;
    message: string;
    severity: "success" | "error" | "warning" | "info";
  }>({ open: false, message: "", severity: "success" });
  const navigate = useNavigate();

  const columns = useMemo<Column<GuestDto>[]>(
    () => [
      // { id: "primaryGuestName", label: "Tên khách đặt đơn", minWidth: 160 },
      { id: "fullName", label: "Họ tên khách ở", minWidth: 160 },
      { id: "phone", label: "Điện thoại", minWidth: 140 },
      // { id: "checkInDate", label: "Ngày đến", minWidth: 140 },
      // { id: "checkOutDate", label: "Ngày đi", minWidth: 140 },
      { id: "idCard", label: "CMND/CCCD", minWidth: 140 },
      // {
      //   id: "idCardFrontImageUrl",
      //   label: "Mặt trước",
      //   minWidth: 120,
      //   render: (row) => {
      //     const url = row.idCardFrontImageUrl || "";
      //     if (!url) return "-";
      //     const isPdf = url.toLowerCase().endsWith(".pdf");
      //     return isPdf ? (
      //       <Button
      //         size="small"
      //         variant="outlined"
      //         href={url}
      //         target="_blank"
      //         rel="noopener noreferrer"
      //       >
      //         Xem PDF
      //       </Button>
      //     ) : (
      //       <img
      //         src={url}
      //         alt="CCCD trước"
      //         style={{ height: 40, borderRadius: 4 }}
      //       />
      //     );
      //   },
      // },
      // {
      //   id: "idCardBackImageUrl",
      //   label: "Mặt sau",
      //   minWidth: 120,
      //   render: (row) => {
      //     const url = row.idCardBackImageUrl || "";
      //     if (!url) return "-";
      //     const isPdf = url.toLowerCase().endsWith(".pdf");
      //     return isPdf ? (
      //       <Button
      //         size="small"
      //         variant="outlined"
      //         href={url}
      //         target="_blank"
      //         rel="noopener noreferrer"
      //       >
      //         Xem PDF
      //       </Button>
      //     ) : (
      //       <img
      //         src={url}
      //         alt="CCCD sau"
      //         style={{ height: 40, borderRadius: 4 }}
      //       />
      //     );
      //   },
      // },
    ],
    [],
  );

  const fetchItems = async (p = 1) => {
    setLoading(true);
    try {
      const res = await guestsApi.list({
        page: p,
        pageSize,
        name: searchText || undefined,
        fromDate: fromDate
          ? fromDate.startOf("day").format("YYYY-MM-DDTHH:mm:ss")
          : undefined,
        toDate: toDate
          ? toDate.endOf("day").format("YYYY-MM-DDTHH:mm:ss")
          : undefined,
      });
      if (res.isSuccess) {
        setItems(res.data);
        setTotal(res.meta?.total ?? res.data.length);
        setPage(res.meta?.page ?? p);
      } else {
        setSnackbar({
          open: true,
          message: res.message || "Không thể tải danh sách khách",
          severity: "error",
        });
      }
    } catch {
      setSnackbar({
        open: true,
        message: "Đã xảy ra lỗi khi tải danh sách khách",
        severity: "error",
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchItems(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    fetchItems(1);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchText, fromDate, toDate]);

  const onPageChange = (newPage: number) => {
    setPage(newPage);
    fetchItems(newPage);
  };

  const handleAdd = () => {
    setCreateOpen(true);
  };
  const handleEdit = (row: GuestDto) => {
    setEditingItem(row);
    setEditOpen(true);
  };
  const handleView = (row: GuestDto) => {
    setViewItem(row);
    setViewOpen(true);
  };

  return (
    <Box>
      <PageTitle
        title="Quản lý khách"
        subtitle="Tra cứu và cập nhật thông tin khách"
      />
      <Stack
        direction={{ xs: "column", md: "row" }}
        justifyContent={"space-between"}
        alignItems={"center"}
        mb={1}
      >
        <Stack
          direction={{ xs: "column", md: "row" }}
          spacing={1}
          alignItems="center"
          sx={{ mb: 1 }}
        >
          <TextField
            label="Tìm kiếm"
            size={"small"}
            value={searchText}
            onChange={(v) => setSearchText(v.target.value)}
            sx={{ minWidth: 280 }}
          />
          <DatePicker
            label="Từ ngày"
            value={fromDate}
            onChange={(v) => setFromDate(v)}
            slotProps={{ textField: { size: "small" } }}
          />
          <DatePicker
            label="Đến ngày"
            value={toDate}
            onChange={(v) => setToDate(v)}
            slotProps={{ textField: { size: "small" } }}
          />
        </Stack>
        <Button variant="contained" startIcon={<Add />} onClick={handleAdd}>
          Thêm mới
        </Button>
      </Stack>

      <DataTable
        title="Danh sách khách"
        columns={columns}
        data={items}
        loading={loading}
        pagination={{ page, pageSize, total, onPageChange }}
        onView={handleView}
        onEdit={handleEdit}
        renderActions={(row) => (
          <IconButton size="small" onClick={() => navigate(`${row.id}`)}>
            <History fontSize="small" />
          </IconButton>
        )}
        getRowId={(row) => row.id}
      />

      <GuestFormModal
        open={createOpen}
        mode="create"
        initial={undefined}
        onClose={() => setCreateOpen(false)}
        onSubmit={async (payload: CreateGuestRequest) => {
          try {
            const res = await guestsApi.create(payload);
            if (res.isSuccess) {
              setSnackbar({
                open: true,
                message: "Thêm khách thành công",
                severity: "success",
              });
              setCreateOpen(false);
              fetchItems(1);
            } else {
              setSnackbar({
                open: true,
                message: res.message || "Không thể tạo khách",
                severity: "error",
              });
            }
          } catch {
            setSnackbar({
              open: true,
              message: "Đã xảy ra lỗi khi tạo khách",
              severity: "error",
            });
          }
        }}
      />

      <GuestFormModal
        open={editOpen}
        mode="edit"
        initial={editingItem || undefined}
        onClose={() => setEditOpen(false)}
        onSubmit={async (payload: UpdateGuestRequest) => {
          if (!editingItem) return;
          try {
            const res = await guestsApi.update(editingItem.id, payload);
            if (res.isSuccess) {
              setSnackbar({
                open: true,
                message: "Cập nhật khách thành công",
                severity: "success",
              });
              setEditOpen(false);
              fetchItems(page);
            } else {
              setSnackbar({
                open: true,
                message: res.message || "Không thể cập nhật khách",
                severity: "error",
              });
            }
          } catch {
            setSnackbar({
              open: true,
              message: "Đã xảy ra lỗi khi cập nhật khách",
              severity: "error",
            });
          }
        }}
      />
      <Dialog
        open={viewOpen}
        onClose={() => setViewOpen(false)}
        fullWidth
        maxWidth="sm"
      >
        <DialogTitle>Chi tiết khách</DialogTitle>
        <DialogContent>
          {viewItem ? (
            <Stack spacing={2}>
              <Typography variant="subtitle2" fontWeight={700}>
                {viewItem.fullName}
              </Typography>
              <Typography variant="body2">
                Điện thoại: {viewItem.phone || "-"}
              </Typography>
              <Typography variant="body2">
                Email: {viewItem.email || "-"}
              </Typography>
              <Typography variant="body2">
                CMND/CCCD: {viewItem.idCard || "-"}
              </Typography>
              <Stack spacing={2} alignItems="center">
                {viewItem.idCardFrontImageUrl ? (
                  viewItem.idCardFrontImageUrl
                    .toLowerCase()
                    .endsWith(".pdf") ? (
                    <Button
                      variant="outlined"
                      href={viewItem.idCardFrontImageUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Xem CCCD mặt trước (PDF)
                    </Button>
                  ) : (
                    <img
                      src={viewItem.idCardFrontImageUrl}
                      alt="Mặt trước"
                      style={{
                        width: "100%",
                        height: "auto",
                        borderRadius: 8,
                        border: "1px solid #ddd",
                      }}
                    />
                  )
                ) : null}
                {viewItem.idCardBackImageUrl ? (
                  viewItem.idCardBackImageUrl.toLowerCase().endsWith(".pdf") ? (
                    <Button
                      variant="outlined"
                      href={viewItem.idCardBackImageUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      Xem CCCD mặt sau (PDF)
                    </Button>
                  ) : (
                    <img
                      src={viewItem.idCardBackImageUrl}
                      alt="Mặt sau"
                      style={{
                        width: "100%",
                        height: "auto",
                        borderRadius: 8,
                        border: "1px solid #ddd",
                      }}
                    />
                  )
                ) : null}
              </Stack>
            </Stack>
          ) : null}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setViewOpen(false)}>Đóng</Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={snackbar.open}
        autoHideDuration={4000}
        onClose={() => setSnackbar({ ...snackbar, open: false })}
      >
        <Alert
          onClose={() => setSnackbar({ ...snackbar, open: false })}
          severity={snackbar.severity}
          variant="filled"
        >
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};

export default GuestsManagementPage;
