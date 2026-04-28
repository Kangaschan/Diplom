export interface ProfileDto {
  id: string;
  username: string;
  email: string;
  firstName?: string;
  lastName?: string;
  avatarUrl?: string;
  hasActivePremium: boolean;
}
