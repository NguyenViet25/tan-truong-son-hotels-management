import {
  Alert,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Snackbar,
} from "@mui/material";
import React, { useEffect, useState } from "react";
import menusApi, {
  type CreateMenuItemRequest,
  type MenuGroupDto,
  type MenuItemDto,
  type UpdateMenuItemRequest,
} from "../../../../api/menusApi";
import PageTitle from "../../../../components/common/PageTitle";
import MenuItemFormModal from "./components/MenuItemFormModal";
import MenuTable from "./components/MenuTable";
import {
  Stack,
  TextField,
  MenuItem,
  ToggleButtonGroup,
  ToggleButton,
  Grid,
  Chip,
  IconButton,
  Typography,
  Card,
  CardContent,
  InputAdornment,
} from "@mui/material";
import FastfoodIcon from "@mui/icons-material/Fastfood";
import TableRestaurantIcon from "@mui/icons-material/TableRestaurant";
import EditIcon from "@mui/icons-material/Edit";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutline";
import AddIcon from "@mui/icons-material/Add";
import { CardMembership, TableChart } from "@mui/icons-material";
import { Search as SearchIcon } from "@mui/icons-material";
import EmptyState from "../../../../components/common/EmptyState";

// Menu Management Page implementing UC-45 to UC-48
// - UC-45: View menu list with filters (group, shift, status, active)
// - UC-46: Add dish
// - UC-47: Edit dish
// - UC-48: Delete dish (server enforces order-history rule)

const MenuManagementPage: React.FC = () => {
  const [searchTerm, setSearchTerm] = useState<string>("");
  const [items, setItems] = useState<MenuItemDto[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [groups, setGroups] = useState<MenuGroupDto[]>([]);
  const [status, setStatus] = useState<string>("0");
  const [typeFilter, setTypeFilter] = useState<"food" | "set">("food");
  const [viewMode, setViewMode] = useState<"table" | "card">("card");
  const [categoryFilter, setCategoryFilter] = useState<string>(" ");

  // Modal state
  const [createOpen, setCreateOpen] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<MenuItemDto | undefined>(
    undefined,
  );
  const [deleteTarget, setDeleteTarget] = useState<MenuItemDto | undefined>(
    undefined,
  );

  // Notifications
  const [snackbar, setSnackbar] = useState<{
    open: boolean;
    message: string;
    severity: "success" | "error" | "info" | "warning";
  }>({ open: false, message: "", severity: "success" });

  // Fetch menu groups for filters and forms
  const fetchGroups = async () => {
    try {
      const res = await menusApi.getMenuGroups();
      if (res.isSuccess) setGroups(res.data);
    } catch (err) {
      // Silent fail, filters still usable
    }
  };

  // Fetch menu items with applied filters
  const fetchMenuItems = async () => {
    setLoading(true);
    try {
      const qp = {
        status: status || undefined,
        searchTerm: searchTerm || undefined,
      };
      const res = await menusApi.getMenuItems(qp);
      if (res.isSuccess) {
        setItems(res.data);
      } else {
        setSnackbar({
          open: true,
          message: res.message || "Không thể tải danh sách món",
          severity: "error",
        });
      }
    } catch (err) {
      setSnackbar({
        open: true,
        message: "Đã xảy ra lỗi khi tải danh sách món",
        severity: "error",
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchGroups();
    fetchMenuItems();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    fetchMenuItems();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [status, searchTerm]);

  const openCreate = () => setCreateOpen(true);
  const openEdit = (record: MenuItemDto) => {
    setEditingItem(record);
    setEditOpen(true);
  };
  const openDelete = (record: MenuItemDto) => setDeleteTarget(record);

  const closeCreate = () => setCreateOpen(false);
  const closeEdit = () => {
    setEditOpen(false);
    setEditingItem(undefined);
  };

  const createSubmit = async (payload: CreateMenuItemRequest) => {
    try {
      const res = await menusApi.createMenuItem(payload);
      if (res.isSuccess) {
        setSnackbar({
          open: true,
          message: "Tạo món thành công",
          severity: "success",
        });
        closeCreate();
        fetchMenuItems();
      } else {
        setSnackbar({
          open: true,
          message: res.message || "Không thể tạo món",
          severity: "error",
        });
      }
    } catch (err) {
      setSnackbar({
        open: true,
        message: "Đã xảy ra lỗi khi tạo món",
        severity: "error",
      });
    }
  };

  const editSubmit = async (payload: UpdateMenuItemRequest | any) => {
    if (!editingItem) return;
    try {
      const cast: UpdateMenuItemRequest = {
        name: payload?.name,
        description: payload?.description,
        unitPrice: payload?.unitPrice,
        imageUrl: payload?.imageUrl,
        status:
          payload?.status !== undefined ? Number(payload.status) : undefined,
        isActive: payload?.isActive,
      };
      const res = await menusApi.updateMenuItem(editingItem.id, cast);
      if (res.isSuccess) {
        setSnackbar({
          open: true,
          message: "Cập nhật thành công",
          severity: "success",
        });
        closeEdit();
        fetchMenuItems();
      } else {
        setSnackbar({
          open: true,
          message: res.message || "Không thể cập nhật",
          severity: "error",
        });
      }
    } catch (err) {
      setSnackbar({
        open: true,
        message: "Đã xảy ra lỗi khi cập nhật",
        severity: "error",
      });
    }
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    try {
      const res = await menusApi.deleteMenuItem(deleteTarget.id);
      if (res.isSuccess) {
        setSnackbar({
          open: true,
          message: "Xóa thành công",
          severity: "success",
        });
        setDeleteTarget(undefined);
        fetchMenuItems();
      } else {
        setSnackbar({
          open: true,
          message: res.message || "Không thể xóa",
          severity: "error",
        });
      }
    } catch (err) {
      setSnackbar({
        open: true,
        message: "Đã xảy ra lỗi khi xóa",
        severity: "error",
      });
    }
  };

  const foodItems: MenuItemDto[] = React.useMemo(
    () => items.filter((it) => (it.category || "").trim() !== "Set"),
    [items],
  );
  const setRecords: MenuItemDto[] = React.useMemo(
    () => items.filter((it) => (it.category || "").trim() === "Set"),
    [items],
  );
  const sortedSetRecords: MenuItemDto[] = React.useMemo(
    () =>
      [...setRecords].sort((a, b) => Number(a.unitPrice) - Number(b.unitPrice)),
    [setRecords],
  );
  const setGroupsByPrice: Array<[number, MenuItemDto[]]> = React.useMemo(() => {
    const map = new Map<number, MenuItemDto[]>();
    setRecords.forEach((it) => {
      const price = Number(it.unitPrice) || 0;
      const arr = map.get(price) || [];
      arr.push(it);
      map.set(price, arr);
    });
    return Array.from(map.entries()).sort((a, b) => a[0] - b[0]);
  }, [setRecords]);
  const foodItemsFiltered: MenuItemDto[] = React.useMemo(
    () =>
      foodItems.filter(
        (it) =>
          !categoryFilter.trim() ||
          (it.category || "").toUpperCase() ===
            categoryFilter.trim().toUpperCase(),
      ),
    [foodItems, categoryFilter],
  );

  return (
    <Box>
      <Stack
        direction={{ xs: "column", lg: "row" }}
        justifyContent={"space-between"}
        sx={{ mb: 2 }}
        spacing={2}
      >
        <PageTitle
          title="Quản lý thực đơn"
          subtitle="Xem, thêm, sửa, xóa món"
        />
        <Box>
          <Button
            variant="contained"
            color="primary"
            startIcon={<AddIcon />}
            onClick={openCreate}
          >
            {typeFilter === "set" ? "Thêm set" : "Thêm món"}
          </Button>
        </Box>
      </Stack>

      <Stack direction={{ xs: "column", lg: "row" }} mb={2} spacing={2}>
        <ToggleButtonGroup
          size="small"
          value={viewMode}
          exclusive
          onChange={(_, v) => setViewMode(v ?? viewMode)}
        >
          <ToggleButton value="table">
            <TableChart sx={{ mr: 1 }} fontSize="small" />
            Bảng
          </ToggleButton>
          <ToggleButton value="card">
            <CardMembership sx={{ mr: 1 }} fontSize="small" />
            Thẻ
          </ToggleButton>
        </ToggleButtonGroup>

        <TextField
          select
          label="Xem theo"
          size="small"
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value as any)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="food">Theo món</MenuItem>
          <MenuItem value="set">Theo set</MenuItem>
        </TextField>
        {typeFilter === "food" && (
          <TextField
            select
            label="Nhóm món"
            size="small"
            value={categoryFilter}
            onChange={(e) => setCategoryFilter(e.target.value)}
            sx={{ minWidth: 200 }}
          >
            <MenuItem value=" ">Tất cả</MenuItem>
            {FOOD_CATEGORY_VALUES.map((val) => (
              <MenuItem key={val} value={val}>
                {capitalizeWords(val)}
              </MenuItem>
            ))}
          </TextField>
        )}
        <TextField
          select
          label="Trạng thái"
          size="small"
          value={status}
          onChange={(e) => setStatus(e.target.value)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="0">Đang bán</MenuItem>
          <MenuItem value="1">Ngừng bán</MenuItem>
        </TextField>
        <TextField
          label="Tìm kiếm"
          size="small"
          value={searchTerm}
          placeholder="Tìm kiếm món"
          onChange={(e) => setSearchTerm(e.target.value.toUpperCase())}
          fullWidth
          InputProps={{
            startAdornment: (
              <InputAdornment position="start">
                <SearchIcon />
              </InputAdornment>
            ),
          }}
        />
      </Stack>

      {viewMode === "table" ? (
        (typeFilter === "food" ? foodItemsFiltered : setRecords).length === 0 &&
        !loading ? (
          <EmptyState
            title={typeFilter === "food" ? "Không có món" : "Không có set"}
            description={
              "Không tìm thấy kết quả phù hợp. Thử thay đổi bộ lọc hoặc từ khóa."
            }
            icon={
              typeFilter === "food" ? (
                <FastfoodIcon color="disabled" sx={{ fontSize: 40 }} />
              ) : (
                <TableRestaurantIcon color="disabled" sx={{ fontSize: 40 }} />
              )
            }
            height={240}
          />
        ) : (
          <MenuTable
            data={typeFilter === "food" ? foodItemsFiltered : setRecords}
            loading={loading}
            onEdit={openEdit}
            // onDelete={openDelete}
            isFood={typeFilter === "food"}
          />
        )
      ) : (
        <Grid container spacing={3}>
          {typeFilter === "food" ? (
            <Grid size={{ xs: 12 }}>
              <Stack spacing={2}>
                <Stack direction="row" alignItems="center" spacing={1}>
                  <FastfoodIcon color="primary" />
                  <Typography variant="h6">Theo món</Typography>
                  <Chip
                    label={`${foodItemsFiltered.length}`}
                    color="primary"
                    size="small"
                  />
                </Stack>
                {foodItemsFiltered.length === 0 && !loading ? (
                  <EmptyState
                    title="Không có món"
                    description="Không tìm thấy kết quả phù hợp. Thử thay đổi bộ lọc hoặc từ khóa."
                    icon={
                      <FastfoodIcon color="disabled" sx={{ fontSize: 40 }} />
                    }
                  />
                ) : (
                  <Grid container spacing={2}>
                    {foodItemsFiltered.map((it) => (
                      <Grid key={it.id} size={{ xs: 12, md: 6, lg: 3 }}>
                        <Card
                          elevation={0}
                          sx={{
                            borderRadius: 3,
                            p: 2,
                            backgroundColor: "#F5FAEE",
                            boxShadow: "0 4px 16px rgba(0,0,0,0.06)",
                          }}
                        >
                          <Stack spacing={1}>
                            <Box
                              sx={{
                                display: "flex",
                                justifyContent: "center",
                                alignItems: "center",
                              }}
                            >
                              <img
                                src={it.imageUrl || "/assets/logo.png"}
                                alt={it.name}
                                style={{
                                  width: "100%",
                                  height: 160,
                                  objectFit: "contain",
                                }}
                              />
                            </Box>
                            <Typography
                              variant="subtitle1"
                              sx={{ fontWeight: 700 }}
                            >
                              {it.name}
                            </Typography>
                            <Stack
                              direction="row"
                              spacing={1}
                              sx={{ flexWrap: "wrap" }}
                            >
                              <Chip
                                label={it.category || ""}
                                color="success"
                                size="small"
                                variant="outlined"
                              />
                              <Chip
                                label={
                                  it.status == 0 ? "Đang bán" : "Ngừng bán"
                                }
                                color={it.status == 0 ? "success" : "error"}
                                size="small"
                              />
                            </Stack>
                            <Typography variant="body2" color="text.secondary">
                              {it.description || ""}
                            </Typography>
                            <Stack
                              direction="row"
                              alignItems="center"
                              justifyContent="space-between"
                              sx={{ mt: 1 }}
                            >
                              <Typography variant="h6" sx={{ fontWeight: 800 }}>
                                {`${Number(it.unitPrice).toLocaleString()} ₫`}
                              </Typography>
                              <Stack
                                direction="row"
                                spacing={1}
                                alignItems="center"
                              >
                                <IconButton
                                  size="small"
                                  color="primary"
                                  onClick={() => openEdit(it)}
                                >
                                  <EditIcon fontSize="small" />
                                </IconButton>
                                {/* <IconButton
                                  size="small"
                                  color="error"
                                  onClick={() => openDelete(it)}
                                >
                                  <DeleteOutlineIcon fontSize="small" />
                                </IconButton> */}
                              </Stack>
                            </Stack>
                          </Stack>
                        </Card>
                      </Grid>
                    ))}
                  </Grid>
                )}
              </Stack>
            </Grid>
          ) : (
            <Grid size={{ xs: 12 }}>
              <Stack spacing={2}>
                <Stack direction="row" alignItems="center" spacing={1}>
                  <TableRestaurantIcon color="success" />
                  <Typography variant="h6">Theo set</Typography>
                  <Chip
                    label={`${setRecords.length}`}
                    color="success"
                    size="small"
                  />
                </Stack>
                {setRecords.length === 0 && !loading ? (
                  <EmptyState
                    title="Không có set"
                    description="Không tìm thấy kết quả phù hợp. Thử thay đổi bộ lọc hoặc từ khóa."
                    icon={
                      <TableRestaurantIcon
                        color="disabled"
                        sx={{ fontSize: 40 }}
                      />
                    }
                  />
                ) : (
                  <Grid container spacing={2}>
                    {sortedSetRecords.map((it, idx, arr) => {
                      const prevPrice =
                        idx > 0 ? Number(arr[idx - 1].unitPrice) || 0 : -1;
                      const price = Number(it.unitPrice) || 0;
                      const showHeader = prevPrice !== price;
                      return (
                        <React.Fragment key={it.id}>
                          {showHeader && (
                            <Grid size={{ xs: 12 }}>
                              <Stack
                                direction="row"
                                alignItems="center"
                                spacing={1}
                              >
                                <TableRestaurantIcon color="warning" />
                                <Typography
                                  variant="subtitle1"
                                  sx={{ fontWeight: 800 }}
                                >
                                  {`Giá/người: ${Number(
                                    price,
                                  ).toLocaleString()} ₫`}
                                </Typography>
                              </Stack>
                            </Grid>
                          )}
                          <Grid size={{ xs: 12, lg: 6 }}>
                            <Card
                              elevation={0}
                              sx={{
                                borderRadius: 3,
                                p: 2,
                                backgroundColor: "#FFF8E1",
                                boxShadow: "0 4px 16px rgba(0,0,0,0.06)",
                                position: "relative",
                              }}
                            >
                              <Stack
                                direction={{ xs: "column", md: "row" }}
                                spacing={2}
                              >
                                <Stack
                                  justifyContent={"center"}
                                  sx={{ height: "100%" }}
                                >
                                  <Box
                                    sx={{
                                      width: 240,
                                      minWidth: 240,
                                      height: 240,
                                      borderRadius: 2,
                                      overflow: "hidden",
                                    }}
                                  >
                                    <img
                                      src={it.imageUrl || "/assets/logo.png"}
                                      alt={it.name}
                                      style={{
                                        width: "100%",
                                        height: "100%",
                                        objectFit: "contain",
                                      }}
                                    />
                                  </Box>
                                </Stack>

                                <Stack spacing={1} sx={{ flexGrow: 1 }}>
                                  <Stack
                                    direction="row"
                                    alignItems="center"
                                    justifyContent="space-between"
                                  >
                                    <Stack
                                      direction="row"
                                      alignItems="center"
                                      spacing={1}
                                    >
                                      <TableRestaurantIcon color="warning" />
                                      <Typography
                                        variant="subtitle1"
                                        sx={{ fontWeight: 800 }}
                                      >
                                        {it.name}
                                      </Typography>
                                    </Stack>
                                    <Chip
                                      label="Set"
                                      color="warning"
                                      size="small"
                                    />
                                  </Stack>
                                  <Stack spacing={0.5} sx={{ mt: 0.5 }}>
                                    {(it.description || "")
                                      .split(/\n|,/)
                                      .map((s) => s.trim())
                                      .filter((s) => s.length > 0)
                                      .map((food, idx) => (
                                        <Typography
                                          key={idx}
                                          variant="body2"
                                          color="text.secondary"
                                        >
                                          {`${idx + 1}. ${food}`}
                                        </Typography>
                                      ))}
                                  </Stack>
                                  <Stack
                                    direction="row"
                                    justifyContent="space-between"
                                    alignItems="center"
                                  >
                                    <Stack
                                      direction="row"
                                      spacing={1}
                                      alignItems="center"
                                    >
                                      <Chip
                                        label={
                                          it.status == 0
                                            ? "Đang bán"
                                            : "Ngừng bán"
                                        }
                                        color={
                                          it.status == 0 ? "success" : "error"
                                        }
                                        size="small"
                                      />
                                    </Stack>
                                    <Stack direction="row" spacing={0.5}>
                                      <IconButton
                                        size="small"
                                        color="primary"
                                        onClick={() => openEdit(it)}
                                      >
                                        <EditIcon fontSize="small" />
                                      </IconButton>
                                      {/* <IconButton
                                    size="small"
                                    color="error"
                                    onClick={() => openDelete(it)}
                                  >
                                    <DeleteOutlineIcon fontSize="small" />
                                  </IconButton> */}
                                    </Stack>
                                  </Stack>
                                </Stack>
                              </Stack>
                              <Chip
                                label={`Giá/người: ${Number(
                                  it.unitPrice,
                                ).toLocaleString()} ₫`}
                                color="warning"
                                sx={{
                                  position: "absolute",
                                  left: 16,
                                  bottom: 16,
                                  fontWeight: 700,
                                }}
                              />
                            </Card>
                          </Grid>
                        </React.Fragment>
                      );
                    })}
                  </Grid>
                )}
              </Stack>
            </Grid>
          )}
        </Grid>
      )}

      {/* Create modal */}
      <MenuItemFormModal
        open={createOpen}
        onClose={closeCreate}
        onSubmit={createSubmit}
        menuGroups={groups}
        mode="create"
        createType={typeFilter}
      />

      {/* Edit modal */}
      <MenuItemFormModal
        open={editOpen}
        onClose={closeEdit}
        onSubmit={editSubmit}
        initialValues={editingItem}
        mode="edit"
        createType={editingItem?.category === "Set" ? "set" : "food"}
      />

      {/* Delete confirm */}
      <Dialog open={!!deleteTarget} onClose={() => setDeleteTarget(undefined)}>
        <DialogTitle>Xóa món</DialogTitle>
        <DialogContent>Bạn có chắc chắn muốn xóa món này?</DialogContent>
        <DialogActions>
          <Button onClick={() => setDeleteTarget(undefined)} color="inherit">
            Hủy
          </Button>
          <Button onClick={confirmDelete} variant="contained" color="error">
            Xóa
          </Button>
        </DialogActions>
      </Dialog>

      {/* Snackbar notifications */}
      <Snackbar
        open={snackbar.open}
        autoHideDuration={3000}
        onClose={() => setSnackbar((s) => ({ ...s, open: false }))}
      >
        <Alert severity={snackbar.severity} sx={{ width: "100%" }}>
          {snackbar.message}
        </Alert>
      </Snackbar>
    </Box>
  );
};

export default MenuManagementPage;
export const FOOD_CATEGORY_VALUES: string[] = [
  "NGAO",
  "HÀU",
  "SÒ LÔNG",
  "BỀ BỀ",
  "SAM",
  "MÓNG TAY",
  "TRAI",
  "TÔM HÙM",
  "TÔM",
  "CUA GẠCH - CUA THỊT",
  "RẮN BIỂN",
  "NEM",
  "SÚP KHAI VỊ",
  "GÀ",
  "THỊT LỢN",
  "THỊT BÒ",
  "LƯƠN",
  "CÁ GIÒ",
  "RAU",
  "CANH - CƠM",
  "GHẸ",
  "CÁ THU",
  "CÁ NỤC",
  "MỰC TƯƠI",
  "ỐC HƯƠNG",
  "TU HÀI",
  "SỨA",
  "CÁ MÚ",
  "CÁ SỦ",
];

export const capitalizeWords = (s: string) =>
  s
    .toLowerCase()
    .split(" ")
    .map((w) => (w ? w[0].toUpperCase() + w.slice(1) : w))
    .join(" ");
