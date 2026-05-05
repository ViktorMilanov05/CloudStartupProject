export interface Company {
  id: string;
  name: string;
  createdAt: string;
  userCount: number;
}

export interface CreateCompanyRequest {
  companyName: string;
  managerEmail: string;
  managerPassword: string;
  managerFirstName: string;
  managerLastName: string;
}
