import http from "./http";

export async function updateMemberProfile(profile) {
  const { data } = await http.put("/member/profile", profile);
  return data;
}
